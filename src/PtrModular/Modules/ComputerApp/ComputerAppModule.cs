using System;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.PcApps;
using PtrLib.Ui;

namespace PtrModular.Modules;

internal sealed class ComputerAppModule : PtrModule
{
    private const string AppId = "PtrModular.ServerBuilder";
    private const string AppLabel = "Server Builder";
    private const string IconName = "Icon Server Builder bcg";

    private HarmonyLib.Harmony? _harmony;
    private ComputerShop? _shop;
    private ServerBuilderApp? _app;
    private bool _appRegistered;
    private bool _warned;

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
        _shop = null;
        _app = null;
        _warned = false;
        PcApps.Reset();
        ServerComponentRegistry.Reset();
    }

    public override void OnUpdate()
    {
        if (!Context.InGameplay) return;

        if (_shop == null)
            _shop = UiFactory.FindSceneComponent<ComputerShop>();

        if (_shop == null || _shop.mainScreen == null)
        {
            if (!_warned)
            {
                _warned = true;
                Context.Log.Warning("ComputerShop was not found; app not installed.");
            }

            return;
        }

        PcApps.EnsureInstalled(_shop, Context.Log);
    }

    public override void OnShutdown()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
