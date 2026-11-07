using PtrLib.Items;
using PtrLib.Mod;
using PtrLib.PcApps;

namespace PtrModular.Modules;

internal sealed class ComputerAppModule : PtrModule
{
    private const string AppId = "PtrModular.ServerBuilder";
    private const string AppLabel = "Server Builder";
    private const string IconName = "Icon Server Builder bcg";

    private ServerBuilderApp? _app;
    private bool _appRegistered;
    private bool _itemsRegistered;

    public override string Id => "ComputerApp";
    public override string DisplayName => "Server Builder";
    public override bool EnabledByDefault => true;

    public override void OnInitialize()
    {
        if (!_itemsRegistered)
        {
            _itemsRegistered = true;
            ModItemRegistry.Register(ServerComponentCatalog.All);
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
    }
}
