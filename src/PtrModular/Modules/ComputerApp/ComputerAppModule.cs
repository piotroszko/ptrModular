using System;
using PtrLib.Mod;
using PtrLib.PcApps;

namespace PtrModular.Modules;

internal sealed class ComputerAppModule : PtrModule
{
    private const string AppId = "PtrModular.ServerBuilder";
    private const string AppLabel = "Server Builder";
    private const string IconName = "Icon Server Builder bcg";

    private HarmonyLib.Harmony? _harmony;
    private ServerBuilderApp? _app;
    private bool _appRegistered;

    public override string Id => "ComputerApp";
    public override string DisplayName => "Server Builder";
    public override bool EnabledByDefault => true;

    public override void OnInitialize()
    {
        try
        {
            _harmony = new HarmonyLib.Harmony("PtrModular.ServerComponents");
            _harmony.PatchAll(typeof(ServerComponentPrefabPatch));
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Server component prefab patch could not be installed: {exception}");
            _harmony?.UnpatchSelf();
            _harmony = null;
        }

        if (_appRegistered) return;
        _appRegistered = true;

        PcApps.Register(new PcAppDefinition(
            AppId,
            AppLabel,
            context => _app = ServerBuilderApp.Build(context),
            opened: () => _app?.Open(),
            iconName: IconName));
    }

    public override void OnGameplayEnter()
    {
        _app = null;
        ServerComponentRegistry.Reset();
    }

    public override void OnShutdown()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
