using System;
using System.Diagnostics;
using System.IO;

class OverlayFsManager
{
    public string BaseDir { get; }
    public string OverlaysRoot { get; }

    public OverlayFsManager(string baseDir, string overlaysRoot)
    {
        BaseDir = baseDir;
        OverlaysRoot = overlaysRoot;
    }

    public string CreateOverlay(string name)
    {
        string upperDir = Path.Combine(OverlaysRoot, name, "upper");
        string workDir = Path.Combine(OverlaysRoot, name, "work");
        string mountDir = Path.Combine(OverlaysRoot, name, "merged");

        Directory.CreateDirectory(upperDir);
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(mountDir);

        string args = $"mount -t overlay overlay -o lowerdir={BaseDir},upperdir={upperDir},workdir={workDir} {mountDir}";
        RunCommand("sudo", args);

        Console.WriteLine($"[INFO] Overlay created at {mountDir}");
        return mountDir;
    }

    public void RemoveOverlay(string name)
    {
        string mountDir = Path.Combine(OverlaysRoot, name, "merged");
        RunCommand("sudo", $"umount {mountDir}");
        Console.WriteLine($"[INFO] Overlay {name} unmounted.");
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
    public void RemoveAllOverlays()
    {
        if (!Directory.Exists(OverlaysRoot))
        {
            Console.WriteLine("[INFO] No overlays root found.");
            return;
        }

        foreach (var dir in Directory.GetDirectories(OverlaysRoot))
        {
            string mountDir = Path.Combine(dir, "merged");
            if (Directory.Exists(mountDir))
            {
                Console.WriteLine($"[INFO] Unmounting overlay at {mountDir}...");
                RunCommand("sudo", $"umount {mountDir}");
            }
        }

        Console.WriteLine("[INFO] All overlays unmounted.");
    }

}
