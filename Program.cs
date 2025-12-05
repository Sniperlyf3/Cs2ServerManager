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

await InstallSteamCmd();
await InstallOrUpdateCS2();
await InstallOrUpdateMetamod();
await InstallOrUpdateCounterStrikeSharp();
await InstallOrUpdatePlugin("B3none", "cs2-retakes", @"cs2-retakes-.*\..*\..*\.zip");
await InstallOrUpdatePlugin("B3none", "cs2-instaplant", @".*\.zip");
await InstallOrUpdatePlugin("B3none", "cs2-instadefuse", @".*\.zip");
await InstallOrUpdatePlugin("Nereziel", "cs2-WeaponPaints", @"WeaponPaints\.zip");
string gameInfoPath = Path.Combine(ServerDir, "game", "csgo", "gameinfo.gi");
EnsureMetamodInGameInfo(gameInfoPath);

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

async Task InstallOrUpdateCS2(bool validate = true)
{
    Console.WriteLine("[INFO] Installing/updating CS2 server...");
    string cmd =
        $"+login anonymous +force_install_dir \"{ServerDir}\" +app_update 730 {(validate ? "validate" : "")}";
    RunSteamCmd(cmd);
}


async Task InstallOrUpdateMetamod()
{
    Console.WriteLine("[INFO] Installing Metamod:Source...");
    string html = await http.GetStringAsync("https://www.metamodsource.net/downloads.php?branch=stable");
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    var links = doc.DocumentNode.SelectNodes("//a[contains(@class,'quick-download') and contains(@class,'download-link')]");
    if (links == null || links.Count == 0)
        throw new Exception("No download links found on Metamod:Source page.");
    string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
    string href = null;
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
    string zip = await DownloadLatestReleaseAsset("roflmuffin", "CounterStrikeSharp", "counterstrikesharp-with-runtime-windows-.*\\.zip");
    ZipFile.ExtractToDirectory(zip, ModsDir, true);
}

async Task InstallOrUpdatePlugin(string owner, string repo, string assetPattern, string targetSubdir = "addons/CounterStrikeSharp/plugins")
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
