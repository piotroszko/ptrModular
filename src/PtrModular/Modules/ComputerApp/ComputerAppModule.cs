using System;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.Ui;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PtrModular.Modules;

internal sealed class ComputerAppModule : PtrModule
{
    private const string IconName = "Icon Server Builder bcg";
    private const string IconLabel = "Server Builder";

    private HarmonyLib.Harmony? _harmony;
    private ComputerShop? _shop;
    private ServerBuilderApp? _app;
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
    }

    public override void OnGameplayEnter()
    {
        _shop = null;
        _app = null;
        _warned = false;
        ServerComponentRegistry.Reset();
    }

    public override void OnUpdate()
    {


        if (!Context.InGameplay) return;

        if (_app != null)
        {
            if (_app.Panel.activeSelf && _shop != null && _shop.mainScreen != null && _shop.mainScreen.activeSelf)
                _app.Close();

            return;
        }

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

        Install(_shop);
    }

    public override void OnShutdown()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    private void Install(ComputerShop shop)
    {
        var main = shop.mainScreen;
        var grid = main.transform.Find("Button Grid");
        if (grid == null)
        {
            Context.Log.Warning("Computer desktop grid was not found.");
            return;
        }

        if (grid.Find(IconName) != null)
            return;

        var templateIcon = grid.Find("Icon Shop bcg");
        if (templateIcon == null)
        {
            if (grid.childCount == 0)
                return;

            templateIcon = grid.GetChild(0);
        }

        var templateText = templateIcon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (templateText == null)
        {
            Context.Log.Warning("Computer icon text template was not found.");
            return;
        }

        var icon = UnityEngine.Object.Instantiate(templateIcon.gameObject, grid);
        icon.name = IconName;

        var label = icon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            var localised = label.GetComponent<LocalisedText>();
            if (localised != null) localised.enabled = false;
            label.text = IconLabel;
        }

        var button = icon.GetComponentInChildren<ButtonExtended>(true);
        if (button == null)
        {
            UnityEngine.Object.Destroy(icon);
            Context.Log.Warning("Computer icon button was not found.");
            return;
        }

        var returnTemplate = shop.networkMapScreen != null
            ? shop.networkMapScreen.transform.Find("Button Return")
            : main.transform.Find("Button Return");

        var returnButton = returnTemplate != null
            ? returnTemplate.GetComponent<ButtonExtended>()
            : null;

        var app = ServerBuilderApp.Create(
            shop,
            main,
            templateText,
            returnButton ?? button,
            Context.Log);

        if (app == null)
        {
            UnityEngine.Object.Destroy(icon);
            Context.Log.Error("Server Builder app could not be created.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)(Action)(app.Open));

        _app = app;
        Context.Log.Msg("Installed 'Server Builder' icon and shop on the computer desktop.");

        FixScreenDepth(shop);


    }

    private static void FixScreenDepth(ComputerShop shop)
    {
        var root = shop.canvasComputerShop != null
            ? shop.canvasComputerShop
            : shop.mainScreen;
        if (root == null) return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var material = renderer.material;
            if (material == null) continue;
            if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", 4f);
            if (material.HasProperty("_ZTestTransparent"))
                material.SetFloat("_ZTestTransparent", 4f);
            if (material.HasProperty("_ZTestGBuffer"))
                material.SetFloat("_ZTestGBuffer", 4f);
        }

        foreach (var canvasRenderer in root.GetComponentsInChildren<CanvasRenderer>(true))
        {
            try
            {
                var material = canvasRenderer.GetMaterial();
                if (material == null) continue;
                if (material.HasProperty("_ZTest"))
                    material.SetFloat("_ZTest", 4f);
            }
            catch (Exception)
            {
            }
        }
    }

}
