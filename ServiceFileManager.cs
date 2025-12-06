using System;
using System.Diagnostics;
using System.IO;

class ServiceFileManager
{
    public string ServiceDir { get; }

    public ServiceFileManager(string serviceDir = "/etc/systemd/system")
    {
        ServiceDir = serviceDir;
    }

    public string CreateServiceFile(string name, string overlayPath, string? gslt, int port, string description, string? map)
    {
        string serviceName = $"cs2-server-{name}.service";
        string servicePath = Path.Combine(ServiceDir, serviceName);

        string execStart = $"{overlayPath}/cs2/bin/linuxsteamrt64/cs2 " +
                           $"-dedicated -console -usercon " +
                           $"{(gslt != null ? $"+sv_setsteamaccount {gslt} " : "")}" +
                           $"-port {port} " +
                           $"+hostname \"{description}\"" +
                           $"{(map != null ? $"+map {map}" : "")}";

        string serviceContent = $@"
            [Unit]
            Description=CS2 Server ({name})
            After=network.target

            [Service]
            Type=simple
            WorkingDirectory={overlayPath}
            ExecStart={execStart}
            Restart=always
            RestartSec=5
            User=cs2user

            [Install]
            WantedBy=multi-user.target
        ";

        File.WriteAllText(servicePath, serviceContent);
        Console.WriteLine($"[INFO] Service file created: {servicePath}");

        // Reload systemd so it sees the new unit
        RunCommand("sudo", "systemctl daemon-reload");

        return serviceName;
    }

    public void StartService(string name) => RunCommand("sudo", $"systemctl start cs2-server-{name}.service");
    public void StopService(string name) => RunCommand("sudo", $"systemctl stop cs2-server-{name}.service");
    public void RestartService(string name) => RunCommand("sudo", $"systemctl restart cs2-server-{name}.service");
    public void EnableService(string name) => RunCommand("sudo", $"systemctl enable cs2-server-{name}.service");
    public void DisableService(string name) => RunCommand("sudo", $"systemctl disable cs2-server-{name}.service");
    public void DeleteServiceFile(string name)
    {
        string servicePath = Path.Combine(ServiceDir, $"cs2-server-{name}.service");
        if (File.Exists(servicePath))
        {
            StopService(name);
            File.Delete(servicePath);
            Console.WriteLine($"[INFO] Deleted service file: {servicePath}");
            RunCommand("sudo", "systemctl daemon-reload");
        }
    }

    private void RunCommand(string fileName, string arguments)
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

    public void RemoveAllServicesWithPrefix(string prefix = "cs2-server")
    {
        if (!Directory.Exists(ServiceDir))
        {
            Console.WriteLine("[INFO] Service directory not found.");
            return;
        }

        var serviceFiles = Directory.GetFiles(ServiceDir, $"{prefix}*.service");
        if (serviceFiles.Length == 0)
        {
            Console.WriteLine($"[INFO] No services found with prefix '{prefix}'.");
            return;
        }

        foreach (var file in serviceFiles)
        {
            string serviceName = Path.GetFileName(file);
            string shortName = serviceName.Replace(".service", "");

            Console.WriteLine($"[INFO] Stopping {serviceName}...");
            RunCommand("sudo", $"systemctl stop {shortName}");

            Console.WriteLine($"[INFO] Disabling {serviceName}...");
            RunCommand("sudo", $"systemctl disable {shortName}");

            Console.WriteLine($"[INFO] Deleting {file}...");
            File.Delete(file);
        }

        RunCommand("sudo", "systemctl daemon-reload");

        Console.WriteLine($"[INFO] Removed all services with prefix '{prefix}'.");
    }

}
