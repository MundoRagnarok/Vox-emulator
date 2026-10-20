using System.Diagnostics;
using System.Text.RegularExpressions;
using Ro3Emu.Setup.Install;

namespace Ro3Emu.Setup;

/// <summary>RO3Emu.exe install / update: the official client from the publisher's update service.</summary>
internal static class InstallFlow
{
    public static async Task<int> RunAsync(Options options, bool update)
    {
        Ui.Title(update ? "RO3Emu - update the game" : "RO3Emu - download the game");
        string folder;
        if (options.Positional.Count > 0) folder = Path.GetFullPath(options.Positional[0]);
        else if (update) folder = Flows.KnownClient(options) ?? await Flows.ResolveClientAsync(options, offerInstall: false);
        else folder = AskFolder();
        if (update && !options.ServerFiles && !ClientLocator.IsClient(folder))
            throw new SetupException($"'{folder}' has no game to update.", "Run RO3Emu.exe install to download it.");

        if (options.ServerFiles)
        {
            // A server needs the game scripts and the VFS index, then only the VFS files that hold the walk maps.
            await InstallAsync(folder, options, path => path.StartsWith("ro3_Data/StreamingAssets/Recovery/LuaPayload/", StringComparison.Ordinal)
                || path == "ro3_Data/StreamingAssets/VFS/assetmeta.data");
            var vfs = DataExport.GameDataExport.NavSourceFiles(folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Ui.Info($"The walk maps are in {vfs.Count} VFS file(s).");
            await InstallAsync(folder, options, vfs.Contains);
            Ui.Ok($"{folder} has what the server export needs.");
            return 0;
        }
        await InstallAsync(folder, options);
        var settings = Settings.Load();
        if (ClientLocator.IsClient(folder))
        {
            settings.ClientDir = folder;
            settings.Save();
        }
        if (!update && options.Only == null && Ui.Interactive && Ui.Confirm("Start playing on your own server now?"))
            return await Flows.PlayLocalAsync(new OptionsWithClient(options, folder).Value, host: false);
        Ui.PauseIfOwnConsole();
        return 0;
    }

    /// <summary>The "no game found" path of start / join: download into a folder the player confirms.</summary>
    public static async Task<string> InstallNewAsync(Options options)
    {
        var folder = AskFolder();
        await InstallAsync(folder, options);
        return folder;
    }

    private static string AskFolder()
    {
        // The fixed drive with the most free space, in a plain folder (no Program Files: no administrator needed).
        var drive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).OrderByDescending(d => d.AvailableFreeSpace).FirstOrDefault();
        var suggestion = Path.Combine(drive?.RootDirectory.FullName ?? SetupHome.Root, "Games", "Ragnarok3");
        var answer = Ui.Ask("Pasta onde deseja instalar o jogo:", suggestion);
        return Path.GetFullPath(answer.Trim('"'));
    }

    public static async Task InstallAsync(string folder, Options options, Func<string, bool>? only = null)
    {
        var config = PublisherConfig.Load(SetupHome.AppDir);
        using var http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, ConnectTimeout = TimeSpan.FromSeconds(20) })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"RO3Emu/{typeof(InstallFlow).Assembly.GetName().Version}");
        var installer = new ClientInstaller(config, http, SetupHome.DownloadDir) { Log = SetupLog.Write };

        Ui.Step("Asking the official servers for the current release");
        ClientInstaller.Manifests manifests;
        using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            manifests = await installer.FetchAsync(cts.Token);
        var release = manifests.Full.Release;
        Ui.Ok($"{release.Id} (release {release.Sequence}), signature checked ({config.KeyId})");

        var settings = Settings.Load();
        if (settings.InstalledSequence.TryGetValue(folder, out var installed) && installed > release.Sequence)
            throw new InstallException($"The servers offer release {release.Sequence}, older than the {installed} already installed here.",
                "Try again later; RO3Emu does not install an older release over a newer one.");

        Func<string, bool>? include = only ?? (options.Only is { } pattern ? Glob(pattern) : null);
        Ui.Step(include == null ? $"Comparing {manifests.Full.TargetFiles.Count} files with {folder}" : $"Comparing the needed files with {folder}");
        var progress = new ProgressLine();
        installer.Progress = progress.Report;
        var plan = installer.MakePlan(manifests, folder, include);
        Ui.EndProgress();
        Ui.Ok($"{plan.Present} files are already right" + (plan.KeptPatched > 0 ? $" ({plan.KeptPatched} patched by RO3Emu, originals kept)" : "")
              + $", {plan.Needed.Count} to get ({Ui.Size(plan.NeededBytes)})");
        if (plan.Needed.Count == 0)
        {
            Record(folder, release.Sequence);
            Ui.Ok(include == null ? "the game is complete and up to date" : "the needed files are complete and up to date");
            return;
        }

        CheckSpace(folder, plan);
        Ui.Info($"Download: {Ui.Size(plan.DownloadBytes)} in {plan.Packages.Count} package(s) from {manifests.Origin}");
        if (!Ui.Confirm("Download now?")) throw new SetupException("Download cancelled.", "Run the same command again when you are ready.");

        Ui.Step("Downloading the game");
        using var cancel = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, e) => { e.Cancel = true; cancel.Cancel(); };
        Console.CancelKeyPress += stop;
        var watch = Stopwatch.StartNew();
        try { await installer.ApplyAsync(plan, folder, cancel.Token); }
        catch (OperationCanceledException)
        {
            throw new SetupException("Download stopped.", "Run the same command again: it continues where it stopped.");
        }
        finally { Console.CancelKeyPress -= stop; }
        Ui.EndProgress();
        Record(folder, release.Sequence);
        Ui.Ok($"{plan.Needed.Count} files written and checked in {Ui.Duration(watch.Elapsed)}");
    }

    private static void Record(string folder, long sequence)
    {
        var settings = Settings.Load();
        settings.InstalledSequence[folder] = sequence;
        settings.Save();
    }

    /// <summary>
    /// The files plus the largest package must fit (each package is deleted once extracted, so they never pile up),
    /// with a margin; the download folder may sit on another drive.
    /// </summary>
    private static void CheckSpace(string folder, ClientInstaller.Plan plan)
    {
        const long margin = 1L << 30;
        Directory.CreateDirectory(SetupHome.DownloadDir);
        var gameDrive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!);
        var cacheDrive = new DriveInfo(Path.GetPathRoot(SetupHome.DownloadDir)!);
        bool same = string.Equals(gameDrive.Name, cacheDrive.Name, StringComparison.OrdinalIgnoreCase);
        long needGame = plan.NeededBytes + (same ? plan.LargestPackage : 0) + margin;
        if (gameDrive.AvailableFreeSpace < needGame)
            throw new InstallException($"Drive {gameDrive.Name} has {Ui.Size(gameDrive.AvailableFreeSpace)} free; the game needs {Ui.Size(needGame)}.",
                "Free some space or choose a folder on another drive.");
        if (!same && cacheDrive.AvailableFreeSpace < plan.LargestPackage + margin)
            throw new InstallException($"Drive {cacheDrive.Name} (downloads) has {Ui.Size(cacheDrive.AvailableFreeSpace)} free; a package needs {Ui.Size(plan.LargestPackage + margin)}.",
                "Free some space there, or move RO3Emu to the game's drive.");
    }

    /// <summary>A path filter: '*' within a folder, '**' across folders.</summary>
    public static Func<string, bool> Glob(string pattern)
    {
        var regex = new Regex("^" + Regex.Escape(pattern.Replace('\\', '/')).Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$", RegexOptions.IgnoreCase);
        return path => regex.IsMatch(path);
    }

    /// <summary>The download line: done / total, speed and time left.</summary>
    private sealed class ProgressLine
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly Stopwatch _shown = Stopwatch.StartNew();
        private readonly Queue<(double T, long Bytes)> _window = new();
        private string _stage = "";

        public void Report(string stage, long done, long total)
        {
            if (stage != _stage) { _stage = stage; _window.Clear(); }
            double t = _watch.Elapsed.TotalSeconds;
            _window.Enqueue((t, done));
            while (_window.Count > 2 && t - _window.Peek().T > 10) _window.Dequeue();
            if (_shown.ElapsedMilliseconds < 250 && done < total) return;
            _shown.Restart();
            if (total <= 0) { Ui.Progress(stage + "..."); return; }
            var (t0, b0) = _window.Peek();
            double speed = t - t0 > 0.5 ? (done - b0) / (t - t0) : 0;
            var eta = speed > 0 ? Ui.Duration(TimeSpan.FromSeconds((total - done) / speed)) : "...";
            Ui.Progress($"{stage}: {Ui.Size(done)} of {Ui.Size(total)} ({done * 100 / total}%)  {Ui.Size((long)speed)}/s  {eta} left");
        }
    }
}

/// <summary>The same options with the game folder set (after an install, to go on and play).</summary>
internal sealed class OptionsWithClient(Options options, string client)
{
    public Options Value { get; } = Options.Parse(Rebuild(options, client));

    private static string[] Rebuild(Options o, string client)
    {
        var args = new List<string> { "start", "--client", client };
        if (o.Reexport) args.Add("--reexport");
        if (o.NoLaunch) args.Add("--no-launch");
        if (o.Yes) args.Add("--yes");
        if (o.Home != null) args.AddRange(["--home", o.Home]);
        args.AddRange(["--http-port", o.Ports.Http.ToString(), "--micro-port", o.Ports.MicroGate.ToString(), "--game-port", o.Ports.GameGate.ToString()]);
        if (o.Ports.Voice > 0) args.AddRange(["--voice-port", o.Ports.Voice.ToString()]);
        return args.ToArray();
    }
}
