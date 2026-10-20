using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using Ro3Emu.ClientTools;
using Ro3Emu.DataExport;

namespace Ro3Emu.Setup;

/// <summary>The player-facing modes: play locally, host, join, restore, check, export, diagnostics.</summary>
internal static class Flows
{
    private static string? HooksDll => ClientDll("Ro3Emu.ClientHooks");
    private static string? LocDll => ClientDll("Ro3Emu.Loc");

    /// <summary>client\&lt;name&gt;.dll next to VoxEmulador.exe; for developers the repository's Release or Debug build.</summary>
    private static string? ClientDll(string project) =>
        SetupHome.Shipped("client", project + ".dll")
        ?? (SetupHome.RepositoryRoot() is { } repo
            ? new[] { "Release", "Debug" }.Select(c => Path.Combine(repo, "src", project, "bin", c, "netstandard2.1", project + ".dll")).FirstOrDefault(File.Exists)
            : null);

    /// <summary>The game folder already chosen (option, saved, environment), without asking; for diagnostics.</summary>
    public static string? KnownClient(Options options)
    {
        if (ClientLocator.IsClient(options.ClientDir)) return Path.GetFullPath(options.ClientDir!);
        var saved = Settings.Load().ClientDir;
        if (ClientLocator.IsClient(saved)) return saved;
        var env = Environment.GetEnvironmentVariable("RO3_CLIENT_DIR");
        return ClientLocator.IsClient(env) ? env : null;
    }

    /// <summary>
    /// The game folder: --client, else the remembered one, else a search; several found means a choice, none means
    /// a folder picker or the official download.
    /// </summary>
    public static async Task<string> ResolveClientAsync(Options options, bool offerInstall)
    {
        var settings = Settings.Load();
        string? chosen = null;

        bool isTutorial = options.Tutorial
            || Environment.GetEnvironmentVariable("VOX_TUTORIAL") == "1"
            || File.Exists(Path.Combine(SetupHome.AppDir, "modo-tutorial.txt"))
            || (Environment.ProcessPath != null && Path.GetFileNameWithoutExtension(Environment.ProcessPath).Contains("Tutorial", StringComparison.OrdinalIgnoreCase));

        if (!isTutorial)
        {
            if (options.ClientDir != null)
            {
                if (!ClientLocator.IsClient(options.ClientDir))
                    throw new SetupException($"'{options.ClientDir}' is not a Ragnarok 3 game folder (no ro3.exe and ro3_Data\\Managed in it).",
                        "Give the folder that contains ro3.exe, for example --client \"C:\\Games\\Genesis\\ROO_PC\".");
                chosen = Path.GetFullPath(options.ClientDir);
            }
            else if (ClientLocator.IsClient(settings.ClientDir)) chosen = settings.ClientDir;
            else
            {
                Ui.Step("Procurando instalacao do jogo");
                var found = ClientLocator.Candidates(settings);
                if (found.Count == 1) chosen = found[0];
                else if (found.Count > 1)
                {
                    for (int i = 0; i < found.Count; i++) Ui.Info($"[{i + 1}] {found[i]}  ({ClientLocator.InstalledRelease(found[i]) ?? "release unknown"})");
                    var answer = Ui.Ask("Qual delas deseja usar?", "1");
                    chosen = int.TryParse(answer, out var n) && n >= 1 && n <= found.Count ? found[n - 1] : found[0];
                }
                else chosen = await NoClientFoundAsync(options, offerInstall);
            }
        }
        else
        {
            Ui.Step("Modo Tutorial: Procurando instalacao do Ragnarok 3...");
            chosen = await NoClientFoundAsync(options, offerInstall);
        }

        settings = Settings.Load();
        settings.ClientDir = chosen;
        settings.Save();
        Ui.Ok($"Pasta do jogo: {chosen}  ({ClientLocator.InstalledRelease(chosen) ?? "release reconhecida"})");
        return chosen;
    }

    private static async Task<string> NoClientFoundAsync(Options options, bool offerInstall)
    {
        if (!Ui.Interactive)
            throw new SetupException("Nenhuma pasta de instalacao do Ragnarok 3 foi encontrada.",
                "Inicie VoxEmulador.exe com --client \"<pasta com ro3.exe>\", ou execute VoxEmulador.exe install para baixar o jogo.");
        Ui.Warn("Nenhuma pasta de instalacao do Ragnarok 3 foi encontrada neste computador.");
        Ui.Info("[1] Ja possuo o jogo: escolher a pasta manualmente (onde esta o ro3.exe)");
        if (offerInstall) Ui.Info("[2] Baixar o jogo oficial da Genesis (download direto, cerca de 24 GB)");
        Ui.Info("[3] Sair");
        var answer = Ui.Ask("Digite sua escolha:", "1");
        if (answer == "2" && offerInstall)
        {
            var folder = await InstallFlow.InstallNewAsync(options);
            return folder;
        }
        if (answer != "1") throw new SetupException("Nenhuma pasta de jogo foi selecionada.", "Execute o VoxEmulador.exe novamente quando tiver o jogo.");
        var picked = FolderPicker.Pick("Selecione a pasta do Ragnarok 3 (a pasta com ro3.exe)") ?? Ui.Ask("Pasta do jogo:");
        foreach (var candidate in new[] { picked, Path.Combine(picked ?? "", "ROO_PC") })
            if (ClientLocator.IsClient(candidate)) return Path.GetFullPath(candidate!);
        throw new SetupException($"'{picked}' nao contem o arquivo ro3.exe.", "Execute VoxEmulador.exe novamente e selecione a pasta correta com ro3.exe.");
    }

    /// <summary>Stops when the folder cannot work with this RO3Emu, with the reason.</summary>
    private static void CheckClient(string client, bool needExport)
    {
        var problems = ClientLocator.Problems(client, needExport, HooksDll, LocDll);
        foreach (var p in problems) SetupLog.Write("check: " + p);
        if (problems.Count == 0) return;
        var shown = string.Join("\n    ", problems.Take(6)) + (problems.Count > 6 ? $"\n    ... and {problems.Count - 6} more" : "");
        throw new SetupException($"This game folder does not fit this RO3Emu:\n    {shown}",
            "Run VoxEmulador.exe update to repair the game from the official servers. If it still fails, this game version is newer than " +
            "RO3Emu: get the latest RO3Emu from the releases page.");
    }

    /// <summary>Exports the game data when it is missing, from another client version, or asked for.</summary>
    public static void EnsureGameData(string client, bool force)
    {
        Ui.Step("Game data");
        if (!force && GameDataExport.UpToDate(SetupHome.DataDir, client))
        {
            var stamp = GameDataExport.ReadStamp(SetupHome.DataDir)!;
            Ui.Ok($"ready (exported {stamp.ExportedUtc.ToLocalTime():yyyy-MM-dd HH:mm})");
            return;
        }
        Ui.Info("Exporting the game data from your game folder (first run or game update, about a minute).");
        RunExport(client, SetupHome.DataDir);
    }

    private static GameDataExport.Result RunExport(string client, string dataDir)
    {
        var watch = Stopwatch.StartNew();
        var last = Stopwatch.StartNew();
        var result = GameDataExport.Run(client, dataDir, new GameDataExport.Options
        {
            Translations = Translations(),
            TranslationQueue = SetupHome.RepositoryRoot() is { } repo ? Path.Combine(repo, "re", "loc", "translation_queue.tsv") : null,
            Log = SetupLog.Write,
            Progress = (stage, done, total) =>
            {
                if (last.ElapsedMilliseconds < 200 && done != total) return;
                last.Restart();
                Ui.Progress(total > 0 ? $"{stage}: {done * 100 / total}%" : stage + "...");
            },
        });
        Ui.EndProgress();
        Ui.Ok($"exported in {Ui.Duration(watch.Elapsed)}: {result.ConfigTables} tables, {result.SceneEditor} maps, {result.Skills} skills, " +
              $"{result.NavFiles} walk maps, {result.Messages} messages, {result.LocEntries} English texts");
        if (result.FailedTables.Count > 0)
            Ui.Warn($"{result.FailedTables.Count} game table(s) could not be exported ({string.Join(", ", result.FailedTables.Take(5))}" +
                (result.FailedTables.Count > 5 ? ", ..." : "") + "). The server may miss some content; RO3Emu will try again next start. " +
                "If this keeps happening, your game is newer than this RO3Emu: get the latest one from the releases page.");
        return result;
    }

    /// <summary>Our translations, embedded in VoxEmulador.exe, in file name order.</summary>
    public static List<string> Translations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames().Where(n => n.StartsWith("translations/", StringComparison.Ordinal)).Order(StringComparer.Ordinal)
            .Select(n =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(n)!);
                return reader.ReadToEnd();
            }).ToList();
    }

    public static async Task<int> PlayLocalAsync(Options options, bool host)
    {
        Ui.Title(host ? "Vox Emulador - Servidor de Ragnarok 3 (Host)" : "Vox Emulador - Ragnarok 3 no seu Computador");
        Ui.Info("Fork comunitário baseado no projeto ro3emu por aethai (GPL-3.0)");
        Ui.Info("Comunidade Mundo Ragnarok: https://discord.gg/UvvJF4W3yK");
        var client = await ResolveClientAsync(options, offerInstall: true);
        Ui.Step("Checking the game");
        CheckClient(client, needExport: true);
        Ui.Ok("this game version works with RO3Emu");
        EnsureGameData(client, options.Reexport);

        var serverExe = ServerProcess.FindExecutable()
            ?? throw new SetupException("The server is missing (server\\Ro3Emu.Server.exe next to VoxEmulador.exe).",
                SetupHome.AppDir.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                    ? "VoxEmulador.exe was started from inside the zip. Extract the whole zip first (right-click it, Extract All), then run VoxEmulador.exe from the extracted folder."
                    : "Extract the whole RO3Emu zip again, keeping its folders.");
        var ports = options.Ports;
        string publicHost = "127.0.0.1";
        var args = new List<string>();
        var env = new Dictionary<string, string>();
        if (host)
        {
            publicHost = options.PublicHost ?? LanAddress()
                ?? throw new SetupException("This PC has no network address to host on.", "Connect to a network, or give the address with --public <ip>.");
            if (!IPAddress.TryParse(publicHost, out _))
                throw new SetupException($"'{publicHost}' is not an IP address.", "Give an address like 192.168.1.20 or your public IP with --public.");
            var gms = options.GmAccounts.ToList();
            if (gms.Count == 0 && Ui.Interactive)
            {
                var gm = Ui.Ask("Your account name, to get GM commands in game (Enter for nobody):");
                if (gm.Length > 0) gms.Add(gm);
            }
            env["ASPNETCORE_ENVIRONMENT"] = "Production";
            args.AddRange([$"--Ro3Emu:PublicHost={publicHost}", "--Ro3Emu:OpenContent:Enabled=true", "--Ro3Emu:Pass:Play=true", "--Ro3Emu:Pass:FreePremium=true"]);
            for (int i = 0; i < gms.Count; i++) args.Add($"--Ro3Emu:GmAccounts:{i}={gms[i]}");
        }
        else
        {
            // Local play: every account has GM commands (the server's own rule for a non-production loopback server).
            env["ASPNETCORE_ENVIRONMENT"] = "Development";
            args.Add("--Ro3Emu:PublicHost=127.0.0.1");
        }

        Ui.Step("Starting the server");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        ServerProcess? server = null;
        if (!host && await ServerProcess.Answers(http, ports.Http))
            Ui.Ok($"a server is already running on port {ports.Http}; using it");
        else
        {
            if (ServerProcess.BusyPort(ports) is { } busy)
                throw new SetupException($"Port {busy} is already used by another program, so the server cannot start.",
                    "Close the other program (or another RO3Emu window), or start with other ports, for example --http-port 5381 --micro-port 20001 --game-port 33101.");
            server = ServerProcess.Start(serverExe, SetupHome.DataDir, ports, args, env);
            var watch = Stopwatch.StartNew();
            bool ready = await server.WaitReadyAsync(TimeSpan.FromMinutes(5), t => Ui.Progress($"loading the game data... {(int)t.TotalSeconds} s"));
            Ui.EndProgress();
            if (!ready)
            {
                var tail = server.Tail();
                SetupLog.Write("server output:\n" + tail);
                bool exited = server.HasExited;
                server.Dispose();
                throw new SetupException(exited ? "The server stopped while starting:\n" + Indent(LastLines(tail, 6)) : "The server did not answer within 5 minutes.",
                    "Run VoxEmulador.exe again. If it fails the same way, run VoxEmulador.exe start --reexport to export the game data again.");
            }
            Ui.Ok($"running (ready in {Ui.Duration(watch.Elapsed)}, log {server.LogPath})");
        }

        try
        {
            var serverUrl = $"http://{(publicHost.Contains(':') ? "[" + publicHost + "]" : publicHost)}:{ports.Http}";
            var pem = await http.GetStringAsync($"http://127.0.0.1:{ports.Http}/ro3emu/gate-key.pem");
            PatchClient(client, serverUrl, pem);

            if (host)
            {
                Ui.Step("Friends can join");
                Ui.Info($"They run:  VoxEmulador.exe join {publicHost}{(ports.Http == 5380 ? "" : ":" + ports.Http)}");
                Ui.Info($"Windows may ask whether Ro3Emu.Server may use the network: allow it.");
                Ui.Info($"For players outside your network, forward TCP ports {ServerProcess.PortList(ports)} on your router");
                Ui.Info("to this PC and start with --public <your public IP>.");
            }
            if (server != null) Program.OnClose = () => server.Stop(TimeSpan.FromSeconds(4));
            return await PlayAndWaitAsync(client, server, options.NoLaunch);
        }
        finally
        {
            Program.OnClose = null;
            if (server != null)
            {
                bool running = !server.HasExited;
                if (running) Ui.Step("Stopping the server");
                server.Stop(TimeSpan.FromSeconds(30));
                server.Dispose();
                if (running) Ui.Ok($"stopped. Your characters are saved in {SetupHome.DataDir}.");
            }
        }
    }

    /// <summary>Starts the game (unless asked not to) and keeps the server up until the player is done.</summary>
    private static async Task<int> PlayAndWaitAsync(string client, ServerProcess? server, bool noLaunch)
    {
        Process? game = null;
        if (!noLaunch) game = LaunchGame(client);
        Ui.Step("Server running");
        Ui.Info("Keep this window open while you play.");
        if (Ui.Interactive) Ui.Info(noLaunch ? "Press Q to stop the server." : "Press Q to stop the server, or R to start the game again.");
        else Ui.Info("Type quit (or close the window) to stop the server.");
        SetupLog.Write("ready");
        Console.Out.Flush();

        Task<string?>? line = Ui.Interactive ? null : Task.Run(Console.In.ReadLine);
        bool gameAnnounced = false;
        Program.Waiting = true;
        try
        {
            while (!Program.StopRequested)
            {
                if (server is { HasExited: true })
                {
                    SetupLog.Write("server output:\n" + server.Tail(40));
                    throw new SetupException("The server stopped unexpectedly:\n" + Indent(LastLines(server.Tail(), 6)), "Run VoxEmulador.exe again.");
                }
                if (game != null && !gameAnnounced && game.HasExited && !GameRunning())
                {
                    gameAnnounced = true;
                    Ui.Info(Ui.Interactive ? "The game was closed. Press R to start it again, or Q to stop the server." : "The game was closed.");
                }
                if (line != null && line.IsCompleted)
                {
                    var text = line.Result;
                    if (text == null || text.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase)) break;
                    line = Task.Run(Console.In.ReadLine);
                }
                if (Ui.Interactive && Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true).Key;
                    if (key is ConsoleKey.Q or ConsoleKey.Escape) break;
                    if (key == ConsoleKey.R && !GameRunning())
                    {
                        game = LaunchGame(client);
                        gameAnnounced = false;
                    }
                }
                await Task.Delay(300);
            }
        }
        finally { Program.Waiting = false; }
        return 0;
    }

    private static bool GameRunning() => Process.GetProcessesByName("ro3").Any();

    private static Process LaunchGame(string client)
    {
        Ui.Step("Starting the game");
        var game = Quiet(() => Launcher.Start(client, []));
        if (game.HasExited)
        {
            int code = game.ExitCode;
            throw new SetupException($"The game closed right after starting (exit code {code}{(code == 70 ? ", launch check refused" : "")}).",
                "Run VoxEmulador.exe again. If it keeps happening, run VoxEmulador.exe update to repair the game.");
        }
        Ui.Ok("the game is starting. Log in with any account name and password: the account is created on first login.");
        return game;
    }

    /// <summary>Patches the client for the server and writes the English text map next to it.</summary>
    private static void PatchClient(string client, string serverUrl, string pem)
    {
        Ui.Step("Patching the game for this server");
        try { Quiet(() => { ClientPatcher.Patch(client, serverUrl, pem, HooksDll, LocDll); return 0; }); }
        catch (UnauthorizedAccessException e)
        {
            throw new SetupException("The game folder cannot be written: " + e.Message,
                "Close the game if it is running. If the game is under Program Files, run VoxEmulador.exe as administrator once, or move the game to another folder.");
        }
        catch (IOException e) when (e.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
        {
            throw new SetupException("A game file is in use: " + e.Message, "Close the game, then run VoxEmulador.exe again.");
        }
        var map = Path.Combine(SetupHome.DataDir, "loc_en.tsv");
        if (File.Exists(map))
        {
            var target = Path.Combine(client, "ro3emu", "loc_en.tsv");
            bool ours = !File.Exists(target) || File.ReadLines(target).FirstOrDefault()?.StartsWith(ClientPatcher.GeneratedLocHeader, StringComparison.Ordinal) == true;
            if (ours)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(map, target, overwrite: true);
            }
        }
        Ui.Ok($"the game now connects to {serverUrl} (VoxEmulador.exe restore undoes this)");
        Ui.Warn("Do not use the publisher's launcher to repair this game folder while playing on RO3Emu: it undoes the patch.");
    }

    /// <summary>Runs patcher code whose Console output belongs in the log, not on the player's screen.</summary>
    private static T Quiet<T>(Func<T> action)
    {
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try { return action(); }
        finally
        {
            Console.SetOut(original);
            foreach (var line in captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)) SetupLog.Write("  " + line.TrimEnd('\r'));
        }
    }

    public static async Task<int> JoinAsync(Options options)
    {
        Ui.Title("RO3Emu - join a server");
        var address = options.Positional.FirstOrDefault() ?? Ui.Ask("Server address (ask the server owner, for example 203.0.113.10):");
        var serverUrl = ServerUrl(address)
            ?? throw new SetupException($"'{address}' is not a server address.", "Use the address the server owner gave you, for example VoxEmulador.exe join 203.0.113.10");
        var client = await ResolveClientAsync(options, offerInstall: true);
        Ui.Step("Checking the game");
        CheckClient(client, needExport: false);
        Ui.Ok("this game version works with RO3Emu");

        Ui.Step($"Contacting {serverUrl}");
        string pem;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            pem = await http.GetStringAsync(serverUrl + "/ro3emu/gate-key.pem");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new SetupException($"The server at {serverUrl} does not answer ({e.Message}).",
                "Check the address with the server owner, and that the server is running with TCP ports 5380, 20000 and 33100 open to you.");
        }
        Ui.Ok("the server answers");
        EnsureLocMap(client);
        PatchClient(client, serverUrl, pem);
        if (!options.NoLaunch) LaunchGame(client);
        Ui.Info($"Next time, run VoxEmulador.exe join {address} again. This window can be closed.");
        if (Ui.OwnsConsole() && Ui.Interactive) await Task.Delay(8000);
        return 0;
    }

    /// <summary>
    /// Joining runs no export, so data/loc_en.tsv (which <see cref="PatchClient"/> copies next to the game) did not exist
    /// and the client had no English map: every string the client draws in Chinese stayed Chinese. Build the map alone
    /// from the player's own client when it is missing or older than the client's Localization chunks.
    /// </summary>
    internal static void EnsureLocMap(string client)
    {
        var map = Path.Combine(SetupHome.DataDir, "loc_en.tsv");
        var sources = GameDataExport.LocSourceFiles(client);
        if (File.Exists(map) && sources.All(s => File.Exists(s) && File.GetLastWriteTimeUtc(s) <= File.GetLastWriteTimeUtc(map))) return;
        Ui.Step("English texts");
        try
        {
            int entries = GameDataExport.WriteLocMap(client, map, new GameDataExport.Options { Translations = Translations(), Log = SetupLog.Write });
            Ui.Ok($"{entries} English texts made from your game files");
        }
        catch (Exception e) when (e is ExportException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // English text is a comfort, not a requirement: joining still works with the client's own texts.
            SetupLog.Write("English text map not built: " + e);
            Ui.Warn("The English texts could not be made from your game files; some texts may stay in Chinese. " + e.Message);
        }
    }

    /// <summary>"host", "host:port", "[v6]:port" or "http(s)://host[:port]" as a base URL; the port is 5380 unless written.</summary>
    public static string? ServerUrl(string address)
    {
        address = address.Trim().TrimEnd('/');
        if (address.Length == 0) return null;
        var withScheme = address.Contains("://") ? address : "http://" + address;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0
            || uri.AbsolutePath != "/" || uri.Query.Length > 0) return null;
        bool portWritten = System.Text.RegularExpressions.Regex.IsMatch(withScheme, @"^[A-Za-z]+://(\[[^\]]+\]|[^/:\[]+):\d+");
        return $"{uri.Scheme}://{uri.Host}:{(portWritten ? uri.Port : 5380)}";
    }

    public static int Restore(Options options)
    {
        Ui.Title("RO3Emu - restore the game");
        var client = KnownClient(options) ?? throw new SetupException("No game folder is known.", "Run VoxEmulador.exe restore --client \"<folder with ro3.exe>\".");
        Quiet(() => { ClientPatcher.Restore(client); return 0; });
        Ui.Ok($"{client} is back to the original files: it talks to the official servers again.");
        return 0;
    }

    public static int Check(Options options)
    {
        Ui.Title("RO3Emu - check the game");
        var known = KnownClient(options);
        var client = known ?? ResolveClientAsync(options, offerInstall: false).GetAwaiter().GetResult();
        if (known != null) Ui.Info($"{client}  ({ClientLocator.InstalledRelease(client) ?? "release unknown"})");
        var problems = ClientLocator.Problems(client, needExport: true, HooksDll, LocDll);
        foreach (var p in problems) Ui.Warn(p);
        if (problems.Count == 0) Ui.Ok("this game version works with RO3Emu (patch targets and every hook reference found)");
        return problems.Count == 0 ? 0 : 1;
    }

    public static int Export(Options options)
    {
        if (options.Positional.Count < 2) throw new SetupException("export needs <game folder> <data folder>.", "Example: RO3Emu export /srv/ro3/ROO_PC /srv/ro3emu/data");
        var (client, data) = (Path.GetFullPath(options.Positional[0]), Path.GetFullPath(options.Positional[1]));
        if (GameDataExport.Missing(client) is { Count: > 0 } missing)
            throw new SetupException($"'{client}' is not a complete game folder: {string.Join(", ", missing)} missing.",
                "Copy the whole game folder (at least ro3_Data/StreamingAssets) and give the folder that contains ro3_Data.");
        Ui.Step($"Exporting the game data from {client} to {data}");
        var result = RunExport(client, data);
        if (options.ListInputs)
            foreach (var f in result.ClientFilesRead) Console.WriteLine("input " + f);
        return 0;
    }

    public static int Diag(Options options)
    {
        var zip = DiagnosticsZip.Write(KnownClient(options));
        Ui.Ok($"Wrote {zip}");
        Ui.Info($"Attach it to an issue at {Program.IssuesUrl}.");
        Ui.Reveal(zip);
        return 0;
    }

    /// <summary>This PC's LAN IPv4 address: an up interface with a gateway, not loopback or a virtual adapter.</summary>
    public static string? LanAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            var props = nic.GetIPProperties();
            if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) continue;
            var address = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address));
            if (address != null) return address.Address.ToString();
        }
        return null;
    }

    private static string LastLines(string text, int n) => string.Join('\n', text.Split('\n').TakeLast(n));

    private static string Indent(string text) => string.Join('\n', text.Split('\n').Select(l => "    " + l.TrimEnd('\r')));
}
