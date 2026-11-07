using System;
using MelonLoader;
using PtrModular.Ui;
using PtrModular.Modules;

[assembly: MelonInfo(typeof(PtrModular.Mod.PtrModularMod), PtrModular.Mod.ModInfo.Name, PtrModular.Mod.ModInfo.Version, PtrModular.Mod.ModInfo.Author)]
[assembly: MelonGame("Waseku", "Data Center")]

namespace PtrModular.Mod;

public sealed class PtrModularMod : MelonMod
{
    private ModuleHost? _host;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg($"{ModInfo.Name} {ModInfo.Version} initializing (IL2CPP).");

        try
        {
            _host = new ModuleHost(LoggerInstance, new WindowManager());
            foreach (var module in CreateModules())
                _host.Register(module);
            _host.Initialize();
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"PtrModular initialization failed: {exception}");
            _host = null;
        }
    }

    public override void OnUpdate() => _host?.OnUpdate();
    public override void OnGUI() => _host?.DrawGui();
    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => _host?.OnSceneInitialized(sceneName);
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) => _host?.OnSceneUnloaded(sceneName);

    public override void OnDeinitializeMelon()
    {
        _host?.Shutdown();
        _host = null;
    }

    private static PtrModule[] CreateModules() => new PtrModule[]
    {
        new ComputerAppModule(),
        new ServerAssemblyModule(),
    };
}
