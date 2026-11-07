using System;
using Il2Cpp;
using Il2CppTMPro;
using PtrLib.Diagnostics;
using PtrLib.PcApps;
using PtrLib.Ui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PtrModular.Modules;

internal sealed class ServerBuilderApp
{
    private static int _spawnSequence;

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

        ServerComponentRegistry.Refresh();
        SetStatus(ServerComponentRegistry.ResolvedCount == ServerComponentCatalog.All.Length
            ? "Prices are $0 for this phase. Buy a component to spawn it on the shop floor."
            : $"Loaded {ServerComponentRegistry.ResolvedCount}/{ServerComponentCatalog.All.Length} components; check the MelonLoader log.");
    }


    private void Buy(ServerComponent component)
    {
        if (!ServerComponentRegistry.TryGetModId(component.Folder, out int modId, out string reason))
        {
            _log.Warning($"{component.Label} purchase blocked: {reason}");
            SetStatus($"{component.Label} is unavailable: {reason}");
            return;
        }

        try
        {
            var prefab = ServerComponentRegistry.GetPrefab(modId);
            if (prefab == null)
            {
                _log.Warning($"{component.Label} purchase blocked: prefab {modId} was not found.");
                SetStatus($"{component.Label} is unavailable: its prefab was not loaded.");
                return;
            }

            _log.Msg(
                $"Spawning {component.Label} through ComputerShop.SpawnPhysicalItem " +
                $"(modId={modId}, price={component.Price}, type={component.ItemType}).");
            var spawn = _shop.SpawnPhysicalItem(prefab, component.Price, component.ItemType);

            GameObject? item = null;
            int highestUid = -1;
            var items = _shop.spawnedItems;
            if (items != null)
            {
                var enumerator = items.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var pair = enumerator.Current;
                    if (pair.Key <= highestUid) continue;
                    highestUid = pair.Key;
                    item = pair.Value;
                }
            }

            _log.Msg(
                $"SpawnPhysicalItem returned {(spawn.HasValue ? spawn.Value.ToString() : "<none>")}; " +
                $"newest uid={highestUid}, object={(item != null ? item.name : "<none>")}.");

            if (item == null)
            {
                _log.Warning($"{component.Label} produced no spawned item object.");
                SetStatus($"Bought {component.Label}, but the game returned no item.");
                return;
            }

            bool wasActive = item.activeSelf;
            if (!wasActive)
                item.SetActive(true);

            var originalParent = item.transform.parent;
            _log.Msg(
                $"Detaching {item.name} from parent " +
                $"{(originalParent != null ? originalParent.name : "<none>")} " +
                $"(parentActive={(originalParent != null ? originalParent.gameObject.activeInHierarchy.ToString() : "<none>")}).");
            item.transform.SetParent(null, true);

            if (items != null && highestUid >= 0 && items.Remove(highestUid))
                _log.Msg($"Removed uid={highestUid} from ComputerShop.spawnedItems so CloseShop will not destroy it.");

            var usable = item.GetComponent<UsableObject>();
            if (usable != null)
            {
                usable.isModObject = true;
                usable.modFolderName = component.Folder;
                CoopLooseItems.Register(usable);
                ServerComponentRegistry.NormalizeExisting(usable);
            }

            _spawnSequence++;
            MoveToPickupPoint(item, _spawnSequence);

            var itemRenderers = item.GetComponentsInChildren<Renderer>(true);
            var mainRenderer = itemRenderers.Length > 0 ? itemRenderers[0] : null;
            _log.Msg(
                $"{component.Label} item active={wasActive}->{item.activeSelf}, at {item.transform.position}, " +
                $"layer={item.layer}, renderers={itemRenderers.Length}, " +
                $"rendererEnabled={(mainRenderer != null ? mainRenderer.enabled.ToString() : "<none>")}, " +
                $"bounds={(mainRenderer != null ? mainRenderer.bounds.size.ToString() : "<none>")}, " +
                $"usable={usable != null}.");

            foreach (var renderer in itemRenderers)
            {
                var material = renderer.material;
                _log.Msg(
                    $"renderer={renderer.name} layer={renderer.gameObject.layer} " +
                    $"mat={(material != null ? material.name : "<none>")} " +
                    $"shader={(material != null && material.shader != null ? material.shader.name : "<none>")} " +
                    $"queue={(material != null ? material.renderQueue : -1)} " +
                    $"keywords={(material != null ? string.Join(",", material.shaderKeywords) : "<none>")} " +
                    $"color={(material != null ? material.color.ToString() : "<none>")} " +
                    $"outline={(usable != null && usable.outlineEffect != null ? usable.outlineEffect.enabled.ToString() : "none")}");
            }
            _log.Msg(ServerComponentRegistry.NativeServerInfo());
            LogShopState("after spawn");
            LogSpawnedItems();
            SetStatus($"Bought {component.Label}. It is floating in front of you.");
        }
        catch (Exception exception)
        {
            _log.Error($"Purchase of {component.Label} failed.", exception);
            SetStatus($"Purchase of {component.Label} failed; see the MelonLoader log.");
        }
    }

    private void MoveToPickupPoint(GameObject item, int sequence)
    {
        float side = ((sequence - 1) % 4 - 1.5f) * 0.28f;
        var player = PlayerManager.instance;
        Transform? anchor = null;
        if (player != null && player.vcam != null)
            anchor = player.vcam.transform;
        else if (player != null && player.playerGO != null)
            anchor = player.playerGO.transform;

        Vector3 drop;
        if (anchor != null)
        {
            drop = anchor.position
                + anchor.forward * 0.5f
                + anchor.right * side;
            drop.y -= 0.1f;
        }
        else
        {
            var camera = Camera.main;
            if (camera == null) return;

            drop = camera.transform.position
                + camera.transform.forward * 0.8f
                + camera.transform.right * side;
        }

        item.transform.position = drop;

        var body = item.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        _log.Msg($"Placed {item.name} at the player pickup point {drop}.");
    }

    private void LogShopState(string label)
    {
        var inUse = _shop.itemsSpawnsInUse;
        string used = string.Empty;
        if (inUse != null)
        {
            for (int i = 0; i < inUse.Length; i++)
                used += inUse[i] == 0 ? "0" : "1";
        }

        _log.Msg(
            $"{label}: cart={(_shop.cartUIItems != null ? _shop.cartUIItems.Count : -1)}, " +
            $"spawned={(_shop.spawnedItems != null ? _shop.spawnedItems.Count : -1)}, " +
            $"productSpawns={(_shop.transformProductItemsSpawns != null ? _shop.transformProductItemsSpawns.Length : -1)}, " +
            $"bigSpawns={(_shop.transformBigItemSpawns != null ? _shop.transformBigItemSpawns.Length : -1)}, " +
            $"inUse='{used}'.");
    }

    private void LogSpawnedItems()
    {
        var items = _shop.spawnedItems;
        if (items == null) return;

        var enumerator = items.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var pair = enumerator.Current;
            var item = pair.Value;
            _log.Msg(
                $"  spawned uid={pair.Key}: " +
                $"{(item != null ? item.name + " at " + item.transform.position : "<destroyed>")}");
        }
    }


    private void SetStatus(string text)
    {
        _status.text = text;
    }
}
