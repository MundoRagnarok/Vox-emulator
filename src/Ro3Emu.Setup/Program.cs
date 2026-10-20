using System.Text;

namespace Ro3Emu.Setup;

/// <summary>
/// VoxEmulador.exe. Double-clicked it finds (or downloads) the game, exports the game data once, starts the bundled server,
/// patches the client and starts it. See <see cref="Usage"/> for the other modes.
/// </summary>
internal static class Program
{
    public const string IssuesUrl = "https://github.com/MundoRagnarok/Vox-emulator/issues";

    public const string Usage = """
        VoxEmulador.exe [command] [options]

          start              (default) play on your own server on this PC: find the game, export the game data
                             (first run, a few minutes), start the server, patch and start the game
          host               like start, but friends on your network or the Internet can join
                               --public <ip>     the address friends connect to (default: this PC's LAN address)
                               --gm <account>    an account that gets GM commands (repeatable)
          join <address>     play on someone else's server: host, host:port or http://host:port
          install [folder]   download the game from the official Genesis servers (about 24 GB), every file
                             checked against the publisher's signature
          update [folder]    bring the game up to date from the official servers (only changed files)
                               --server-files    only what a server needs for its export (Linux hosts)
          restore            undo the patch (the game talks to the official servers again)
          check              check that the game folder fits this RO3Emu, without changing anything
          export <game> <data>  export the game data only (server hosts, Linux)
          diag               write RO3Emu-diagnostics.zip for a bug report

        options:
          --client <folder>  the game folder (the one with ro3.exe); remembered for next time
          --reexport         export the game data again
          --no-launch        do not start the game (server only)
          --yes              no questions: take the defaults
          --http-port <n> --micro-port <n> --game-port <n>   server ports (default 5380, 20000, 33100)
          --voice-port <n>   turn on party voice chat on this TCP port, for example 33101 (off by default)
          --home <folder>    where RO3Emu keeps data, logs and settings (default: next to VoxEmulador.exe)
        """;

    /// <summary>Ctrl+C or Q while the server runs: the wait ends and the server is stopped cleanly.</summary>
    public static volatile bool StopRequested;

    /// <summary>True while RO3Emu waits with a running server (Ctrl+C then stops it cleanly instead of ending at once).</summary>
    public static volatile bool Waiting;

    /// <summary>Run when the console window is closed: the server gets a few seconds to save and stop.</summary>
    public static Action? OnClose;

    public static async Task<int> Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        Ui.OwnsConsole();
        try { Console.Title = "Vox Emulador - Ragnarok Online 3"; } catch { }
        Console.CancelKeyPress += (_, e) =>
        {
            // While the server runs, the first Ctrl+C stops it cleanly; a second one (or one at any other time) ends RO3Emu.
            if (!Waiting || StopRequested) return;
            StopRequested = true;
            e.Cancel = true;
        };
        // Closing the window (SIGHUP is CTRL_CLOSE_EVENT on Windows): Windows allows about five seconds.
        using var close = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP, _ =>
        {
            SetupLog.Write("window closed");
            try { OnClose?.Invoke(); } catch (Exception e) { SetupLog.Write("close: " + e.Message); }
        });

        Options options;
        try { options = Options.Parse(args); }
        catch (SetupException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Usage);
            return 64;
        }
        if (options.Home != null) SetupHome.Root = options.Home;
        if (options.Yes) Ui.Interactive = false;
        SetupLog.Write($"RO3Emu {typeof(Program).Assembly.GetName().Version} {string.Join(' ', args)} (home {SetupHome.Root})");

        try
        {
            return options.Command switch
            {
                "help" => Help(),
                "start" => await Flows.PlayLocalAsync(options, host: false),
                "host" => await Flows.PlayLocalAsync(options, host: true),
                "join" => await Flows.JoinAsync(options),
                "restore" => Flows.Restore(options),
                "check" => Flows.Check(options),
                "export" => Flows.Export(options),
                "install" => await InstallFlow.RunAsync(options, update: false),
                "update" => await InstallFlow.RunAsync(options, update: true),
                "diag" => Flows.Diag(options),
                _ => throw new SetupException($"Unknown command '{options.Command}'.", "Run VoxEmulador.exe help."),
            };
        }
        catch (SetupException e)
        {
            SetupLog.Write("failed: " + e);
            Ui.Error(e.Message);
            Ui.Info("What to do: " + e.Fix);
            ReportFailure(options);
            return 1;
        }
        catch (Exception e)
        {
            SetupLog.Write("unexpected: " + e);
            Ui.Error("Something unexpected went wrong: " + e.Message);
            ReportFailure(options);
            return 2;
        }
    }

    private static int Help()
    {
        Console.WriteLine(Usage);
        return 0;
    }

    /// <summary>On any failure: the diagnostics zip, where to send it, and a pause so a double-click window stays readable.</summary>
    private static void ReportFailure(Options options)
    {
        try
        {
            var zip = DiagnosticsZip.Write(Flows.KnownClient(options));
            Ui.Info($"Details are in {zip}.");
            Ui.Info($"If you need help, open an issue at {IssuesUrl} and attach that file.");
            Ui.Reveal(zip);
        }
        catch (Exception e) { SetupLog.Write("diagnostics: " + e); }
        Ui.PauseIfOwnConsole();
    }
}

/// <summary>The command line.</summary>
internal sealed class Options
{
    public string Command { get; private set; } = "start";
    public List<string> Positional { get; } = [];
    public string? ClientDir { get; private set; }
    public string? Home { get; private set; }
    public bool Reexport { get; private set; }
    public bool NoLaunch { get; private set; }
    public bool Yes { get; private set; }
    public string? PublicHost { get; private set; }
    public List<string> GmAccounts { get; } = [];
    public string? Only { get; private set; }
    public bool ListInputs { get; private set; }
    public bool ServerFiles { get; private set; }
    public bool Tutorial { get; private set; }
    public ServerProcess.Ports Ports { get; private set; } = new();

    private static readonly HashSet<string> Commands = ["start", "host", "join", "restore", "check", "export", "install", "update", "diag", "help"];

    public static Options Parse(string[] args)
    {
        var o = new Options();
        int i = 0;
        if (args.Length > 0 && !args[0].StartsWith('-'))
        {
            var command = args[0].ToLowerInvariant();
            if (!Commands.Contains(command)) throw new SetupException($"Unknown command '{args[0]}'.", "Run VoxEmulador.exe help.");
            o.Command = command;
            i = 1;
        }
        string Value(string name) => i + 1 < args.Length ? args[++i] : throw new SetupException($"{name} needs a value.", "Run VoxEmulador.exe help.");
        int Port(string name) => int.TryParse(Value(name), out var p) && p is > 0 and < 65536 ? p : throw new SetupException($"{name} needs a port number.", "Run VoxEmulador.exe help.");
        for (; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--client": o.ClientDir = Value("--client"); break;
                case "--home": o.Home = Value("--home"); break;
                case "--reexport": o.Reexport = true; break;
                case "--no-launch": o.NoLaunch = true; break;
                case "--yes": case "-y": o.Yes = true; break;
                case "--public": o.PublicHost = Value("--public"); break;
                case "--gm": o.GmAccounts.Add(Value("--gm")); break;
                case "--only": o.Only = Value("--only"); break;
                case "--list-inputs": o.ListInputs = true; break;
                case "--server-files": o.ServerFiles = true; break;
                case "--tutorial": case "--sem-jogo": o.Tutorial = true; break;
                case "--http-port": o.Ports = o.Ports with { Http = Port("--http-port") }; break;
                case "--micro-port": o.Ports = o.Ports with { MicroGate = Port("--micro-port") }; break;
                case "--game-port": o.Ports = o.Ports with { GameGate = Port("--game-port") }; break;
                case "--voice-port": o.Ports = o.Ports with { Voice = Port("--voice-port") }; break;
                case "-h": case "--help": o.Command = "help"; break;
                default:
                    if (args[i].StartsWith("--")) throw new SetupException($"Unknown option {args[i]}.", "Run VoxEmulador.exe help.");
                    o.Positional.Add(args[i]);
                    break;
            }
        }
        return o;
    }
}
