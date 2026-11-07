using PtrModular.Diagnostics;
using PtrModular.Settings;
using PtrModular.Ui;

namespace PtrModular.Mod;

internal sealed class ModuleContext
{
    internal ModuleContext(
        ModuleLog log,
        ModuleConfig config,
        WindowManager windows,
        ModSettings settings)
    {
        Log = log;
        Config = config;
        Windows = windows;
        Settings = settings;
    }

    internal ModuleLog Log { get; }
    internal ModuleConfig Config { get; }
    internal WindowManager Windows { get; }
    internal ModSettings Settings { get; }
    internal bool InGameplay { get; set; }
}
