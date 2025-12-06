Setup.Setup setup = new();
await setup.PerformBaseSetupAsync();

int portStart = 27015;

OverlayFsManager fsMmanager = new(setup.Directory, "/servers");
ServiceFileManager serviceManager = new();

serviceManager.RemoveAllServicesWithPrefix();
fsMmanager.RemoveAllOverlays();

CreateServer("retakes-dust2", "Tokolossie Retakes - Dust 2", "de_dust2");

void CreateServer(string serverName, string description, string map, string? gslt = null)
{
    var path = fsMmanager.CreateOverlay(serverName);
    var service = serviceManager.CreateServiceFile(serverName, path, gslt, portStart++, description, map);
    serviceManager.EnableService(serverName);
    serviceManager.StartService(service);
}
