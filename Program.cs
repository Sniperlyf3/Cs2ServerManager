using HtmlAgilityPack;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

string BaseDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\cs2" : "/opt/cs2";
string SteamCmdDir = Path.Combine(BaseDir, "steamcmd");
string ServerDir = Path.Combine(BaseDir, "server");
string ModsDir = Path.Combine(ServerDir, "game", "csgo");
HttpClient http = new();

Directory.CreateDirectory(BaseDir);
Directory.CreateDirectory(SteamCmdDir);
Directory.CreateDirectory(ServerDir);
Directory.CreateDirectory(ModsDir);

Console.WriteLine($"[INFO] Starting CS2 setup in {BaseDir}");
Console.WriteLine($"[INFO] ModsDir {ModsDir}");
string gameInfoPath = Path.Combine(ServerDir, "game", "csgo", "gameinfo.gi");

InstallLinuxDependencies();
await InstallSteamCmd();
await InstallOrUpdateCS2();
await InstallOrUpdateMetamod();
await InstallOrUpdateCounterStrikeSharp();
await InstallOrUpdatePlugin("B3none", "cs2-retakes", @"cs2-retakes-\d*\.\d*\.\d*\.zip");
await InstallOrUpdatePlugin("B3none", "cs2-retakes", @"cs2-retakes-shared-.*\.zip", "addons/counterstrikesharp/shared");
await InstallOrUpdatePlugin("B3none", "cs2-instaplant", @".*\.zip");
await InstallOrUpdatePlugin("B3none", "cs2-instadefuse", @".*\.zip");
await InstallOrUpdatePlugin("oscar-wos", "Retakes-Zones", @"Zones\.zip ");
//await InstallOrUpdatePlugin("Nereziel", "cs2-WeaponPaints", @"WeaponPaints\.zip"); TODO: Need more setup, including database, credentials, splitting of gamedata folder and other dependencies (maybe even website?)
EnsureMetamodInGameInfo(gameInfoPath);
EnsureSteamSymlinks();

Console.WriteLine("[INFO] Setup complete. Edit GSLT in start script before launching.");
await Task.Delay(-1);
async Task InstallSteamCmd()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        string exePath = Path.Combine(SteamCmdDir, "steamcmd.exe");
        if (!File.Exists(exePath))
        {
            Console.WriteLine("[INFO] Downloading SteamCMD (Windows)...");
            var zipPath = Path.Combine(SteamCmdDir, "steamcmd.zip");
            var data = await http.GetByteArrayAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
            await File.WriteAllBytesAsync(zipPath, data);
            ZipFile.ExtractToDirectory(zipPath, SteamCmdDir, true);
        }
    }
    else
    {
        string shPath = Path.Combine(SteamCmdDir, "steamcmd.sh");
        if (!File.Exists(shPath))
        {
            Console.WriteLine("[INFO] Downloading SteamCMD (Linux)...");
            var tarPath = Path.Combine(SteamCmdDir, "steamcmd_linux.tar.gz");
            var data = await http.GetByteArrayAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz");
            await File.WriteAllBytesAsync(tarPath, data);
            Process.Start("tar", $"-xzf {tarPath} -C {SteamCmdDir}")?.WaitForExit();
        }
    }
}

void RunSteamCmd(string arguments)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Process.Start(
            Path.Combine(SteamCmdDir, "steamcmd.exe"),
            $"{arguments} +quit"
        )?.WaitForExit();
    }
    else
    {
        Process.Start(
            "bash",
            $"{Path.Combine(SteamCmdDir, "steamcmd.sh")} {arguments} +quit"
        )?.WaitForExit();
    }
}

async Task InstallOrUpdateCS2(bool validate = false)
{
    Console.WriteLine("[INFO] Installing/updating CS2 server...");
    string cmd =
        $"+force_install_dir \"{ServerDir}\" +login anonymous +app_update 730 {(validate ? "validate" : "")}";
    RunSteamCmd(cmd);
}


async Task InstallOrUpdateMetamod()
{
    Console.WriteLine("[INFO] Installing Metamod:Source...");
    string html = await http.GetStringAsync("https://www.metamodsource.net/downloads.php?branch=master");
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    var links = doc.DocumentNode.SelectNodes("//a[contains(@class,'quick-download') and contains(@class,'download-link')]");
    if (links == null || links.Count == 0)
        throw new Exception("No download links found on Metamod:Source page.");
    string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
    string href = string.Empty;
    foreach (var link in links)
    {
        string url = link.GetAttributeValue("href", "");
        if (url.Contains(platform, StringComparison.OrdinalIgnoreCase))
        {
            href = url;
            break;
        }
    }
    if (href == null)
        throw new Exception($"No Metamod download link found for {platform}.");
    string archivePath = Path.Combine(BaseDir, platform == "windows" ? "metamod.zip" : "metamod.tar.gz");
    var data = await http.GetByteArrayAsync(href);
    await File.WriteAllBytesAsync(archivePath, data);
    if (platform == "windows")
    {
        ZipFile.ExtractToDirectory(archivePath, ModsDir, true);
    }
    else
    {
        Process.Start("tar", $"-xzf {archivePath} -C {ModsDir}")?.WaitForExit();
    }
    Console.WriteLine("[INFO] Metamod:Source installed.");
}


async Task InstallOrUpdateCounterStrikeSharp()
{
    Console.WriteLine("[INFO] Installing CounterStrikeSharp...");
    string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
    string zip = await DownloadLatestReleaseAsset("roflmuffin", "CounterStrikeSharp", $"counterstrikesharp-with-runtime-{platform}-.*\\.zip");
    ZipFile.ExtractToDirectory(zip, ModsDir, true);
}

async Task InstallOrUpdatePlugin(string owner, string repo, string assetPattern, string targetSubdir = "addons/counterstrikesharp/plugins")
{
    Console.WriteLine($"[INFO] Installing/updating plugin {owner}/{repo}...");
    string zip = await DownloadLatestReleaseAsset(owner, repo, assetPattern);
    string target = Path.Combine(ModsDir, targetSubdir);
    Directory.CreateDirectory(target);
    ZipFile.ExtractToDirectory(zip, target, true);
}

async Task<string> DownloadLatestReleaseAsset(string owner, string repo, string assetRegex)
{
    string api = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
    http.DefaultRequestHeaders.UserAgent.ParseAdd("cs2-updater");
    var json = await http.GetStringAsync(api);
    using var doc = JsonDocument.Parse(json);
    foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
    {
        string name = asset.GetProperty("name").GetString() ?? "";
        string url = asset.GetProperty("browser_download_url").GetString() ?? "";
        if (Regex.IsMatch(name, assetRegex))
        {
            string outPath = Path.Combine(BaseDir, name);
            var data = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outPath, data);
            return outPath;
        }
    }
    throw new Exception($"No asset matching {assetRegex} found for {owner}/{repo}");
}

void EnsureMetamodInGameInfo(string gameInfoPath)
{
    if (!File.Exists(gameInfoPath))
    {
        Console.WriteLine("[WARN] gameinfo.gi not found at " + gameInfoPath);
        return;
    }
    var lines = File.ReadAllLines(gameInfoPath).ToList();
    string metamodLine = "\t\t\tGame    csgo/addons/metamod";
    if (lines.Any(l => l.Trim().Equals(metamodLine.Trim(), StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("[INFO] Metamod line already present in gameinfo.gi");
        return;
    }
    int index = lines.FindIndex(l => l.Trim().StartsWith("Game_LowViolence", StringComparison.OrdinalIgnoreCase));
    if (index == -1)
    {
        Console.WriteLine("[WARN] Could not find Game_LowViolence line in gameinfo.gi");
        return;
    }
    lines.Insert(index + 1, metamodLine);
    File.WriteAllLines(gameInfoPath, lines);
    Console.WriteLine("[INFO] Added Metamod line to gameinfo.gi");
}

void EnsureSteamSymlinks()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Console.WriteLine("[INFO] Skipping symlink creation (not Linux).");
        return;
    }

    Console.WriteLine("[INFO] Ensuring Steam symlinks...");

    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    string sdk64Dir = Path.Combine(home, ".steam", "sdk64");
    string sdk32Dir = Path.Combine(home, ".steam", "sdk32");

    Directory.CreateDirectory(sdk64Dir);
    Directory.CreateDirectory(sdk32Dir);

    string source64 = Path.Combine(SteamCmdDir, "linux64", "steamclient.so");
    string target64 = Path.Combine(sdk64Dir, "steamclient.so");

    string source32 = Path.Combine(SteamCmdDir, "linux32", "steamclient.so");
    string target32 = Path.Combine(sdk32Dir, "steamclient.so");

    CreateSymlink(source64, target64);
    CreateSymlink(source32, target32);

    Console.WriteLine("[INFO] Steam symlinks created.");
}

void CreateSymlink(string source, string target)
{
    if (File.Exists(target))
    {
        Console.WriteLine($"[INFO] Symlink already exists: {target}");
        return;
    }

    var psi = new ProcessStartInfo
    {
        FileName = "ln",
        Arguments = $"-s \"{source}\" \"{target}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var proc = Process.Start(psi);
    proc?.WaitForExit();

    if (proc?.ExitCode == 0)
        Console.WriteLine($"[INFO] Created symlink: {target} -> {source}");
    else
        Console.WriteLine($"[ERROR] Failed to create symlink {target} -> {source}");
}

void InstallLinuxDependencies()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Console.WriteLine("[INFO] Skipping dependency installation (not Linux).");
        return;
    }

    Console.WriteLine("[INFO] Installing required Linux dependencies for SteamCMD and CS2...");
    RunCommand("sudo", "dpkg --add-architecture i386");
    RunCommand("sudo", "apt-get update");
    string[] packages = new[]
    {
        "tmux",
        "lib32gcc-s1",
        "lib32stdc++6",
        "libcurl4",
        "zlib1g",
        "libtinfo5"
    };

    RunCommand("sudo", $"apt-get install -y {string.Join(" ", packages)}");
    Console.WriteLine("[INFO] Linux dependencies installed successfully.");
}

void RunCommand(string fileName, string arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var proc = Process.Start(psi);
    if (proc != null)
    {
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdout))
            Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            Console.WriteLine(stderr);

        if (proc.ExitCode != 0)
            Console.WriteLine($"[ERROR] Command failed: {fileName} {arguments}");
    }
}
