using System.Diagnostics;

Setup.Setup setup = new();
await setup.PerformBaseSetupAsync();

int portStart = 2888;

OverlayFsManager fsMmanager = new(setup.DirectoryPath, "/servers");
ServiceFileManager serviceManager = new();

serviceManager.RemoveAllServicesWithPrefix();
fsMmanager.RemoveAllOverlays();

CreateServer("retakes-dust2", "Tokolossie Retakes - Dust 2", "de_dust2");

void CreateServer(string serverName, string description, string map, string? gslt = null)
{
    RunCommand("sudo", "chown -R cs2user:cs2user /opt/cs2");
    var path = fsMmanager.CreateOverlay(serverName);
    var service = serviceManager.CreateServiceFile(serverName, path, gslt, portStart++, description, map);
    serviceManager.EnableService(serverName);
    serviceManager.StartService(serverName);
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

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine(stderr);
    }
}