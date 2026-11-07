using Il2Cpp;
using PtrLib.Items;

namespace PtrModular.Modules;

internal static class ServerComponentCatalog
{
    internal static readonly ModItemDefinition[] All =
    {
        new ModItemDefinition(
            "ptr_modular_server_case", "Server Case", 0, PlayerManager.ObjectInHand.Router),
        new ModItemDefinition(
            "ptr_modular_motherboard", "Motherboard", 0, PlayerManager.ObjectInHand.Router),
        new ModItemDefinition(
            "ptr_modular_cpu", "CPU", 0, PlayerManager.ObjectInHand.Router),
        new ModItemDefinition(
            "ptr_modular_gpu", "GPU", 0, PlayerManager.ObjectInHand.Router),
    };
}
