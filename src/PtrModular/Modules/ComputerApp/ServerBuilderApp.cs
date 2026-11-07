using System;
using Il2Cpp;
using Il2CppTMPro;
using PtrLib.Diagnostics;
using PtrLib.Items;
using PtrLib.PcApps;
using PtrLib.Ui;
using UnityEngine;
using UnityEngine.Events;

namespace PtrModular.Modules;

internal sealed class ServerBuilderApp
{
    private readonly ComputerShop _shop;
    private readonly ModuleLog _log;
    private readonly TextMeshProUGUI _status;

    private ServerBuilderApp(ComputerShop shop, ModuleLog log, TextMeshProUGUI status)
    {
        _shop = shop;
        _log = log;
        _status = status;
    }

    internal static ServerBuilderApp Build(PcAppContext context)
    {
        var shop = context.Shop;
        var panel = context.Panel;
        var log = context.Log;

        var canvas = context.MainScreen.GetComponentInParent<Canvas>();
        log.Msg(
            $"Computer desktop canvas: {(canvas != null ? canvas.renderMode.ToString() : "<none>")}, " +
            $"screen='{context.MainScreen.name}'.");

        var buyTemplate = shop.buttonCheckOut;
        if (buyTemplate == null)
        {
            log.Warning("Shop checkout button was not found; using the return button for Buy.");
            buyTemplate = context.ButtonTemplate;
        }

        var title = UiFactory.CreateText(
            panel.transform,
            context.TextTemplate,
            "Server Builder",
            40f,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.zero);
        UiFactory.SetTopLeft(title.rectTransform, new Vector2(600f, 60f), new Vector2(60f, -30f));

        var status = UiFactory.CreateText(
            panel.transform,
            context.TextTemplate,
            string.Empty,
            20f,
            TextAlignmentOptions.Left,
            Vector2.zero,
            Vector2.zero);
        UiFactory.SetTopLeft(status.rectTransform, new Vector2(1200f, 32f), new Vector2(60f, -100f));

        var app = new ServerBuilderApp(shop, log, status);

        for (int i = 0; i < ServerComponentCatalog.All.Length; i++)
        {
            var component = ServerComponentCatalog.All[i];
            float rowY = -160f - i * 64f;

            var name = UiFactory.CreateText(
                panel.transform,
                context.TextTemplate,
                component.Label,
                28f,
                TextAlignmentOptions.Left,
                Vector2.zero,
                Vector2.zero);
            UiFactory.SetTopLeft(name.rectTransform, new Vector2(420f, 48f), new Vector2(60f, rowY));

            var price = UiFactory.CreateText(
                panel.transform,
                context.TextTemplate,
                $"${component.Price}",
                24f,
                TextAlignmentOptions.Left,
                Vector2.zero,
                Vector2.zero);
            UiFactory.SetTopLeft(price.rectTransform, new Vector2(100f, 48f), new Vector2(500f, rowY));

            var buy = UiFactory.CreateButton(
                buyTemplate,
                panel.transform,
                $"Button Buy {component.Label}");
            UiFactory.SetTopLeft(
                buy.GetComponent<RectTransform>(),
                new Vector2(120f, 40f),
                new Vector2(620f, rowY));
            if (!UiFactory.SetButtonLabel(buy, "Buy"))
            {
                var buyText = UiFactory.CreateText(
                    buy.transform,
                    context.TextTemplate,
                    "Buy",
                    20f,
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.zero);
                buyText.raycastTarget = false;
                buyText.transform.SetAsLastSibling();
            }
            UiFactory.SetButtonColors(
                buy,
                new Color(0.10f, 0.42f, 0.62f, 1f),
                Color.white);
            buy.gameObject.SetActive(true);

            var definition = component;
            buy.onClick.AddListener((UnityAction)(Action)(() => app.Buy(definition)));
        }

        return app;
    }

    internal void Open()
    {
        if (_shop.mainScreen == null)
            return;

        ModItemRegistry.Refresh();
        SetStatus(ModItemRegistry.ResolvedCount == ServerComponentCatalog.All.Length
            ? "Prices are $0 for this phase. Buy a component to have it delivered."
            : $"Loaded {ModItemRegistry.ResolvedCount}/{ServerComponentCatalog.All.Length} components; check the MelonLoader log.");
    }

    private void Buy(ModItemDefinition component)
    {
        var result = ModItemSpawner.Spawn(_shop, component, _log);
        SetStatus(result.Message);
    }

    private void SetStatus(string text)
    {
        _status.text = text;
    }
}
