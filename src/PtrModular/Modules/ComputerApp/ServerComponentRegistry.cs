using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace PtrModular.Modules;

internal static class ServerComponentRegistry
{
    private static readonly Dictionary<string, int> ModIdsByFolder = new Dictionary<string, int>();
    private static readonly Dictionary<int, string> FoldersByModId = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LabelsByModId = new Dictionary<int, string>();
    private static readonly Dictionary<int, Sprite> IconsByModId = new Dictionary<int, Sprite>();
    private static readonly HashSet<int> OurModIds = new HashSet<int>();

    internal static int ResolvedCount => ModIdsByFolder.Count;

    internal static void Reset()
    {
        ModIdsByFolder.Clear();
        FoldersByModId.Clear();
        LabelsByModId.Clear();
        IconsByModId.Clear();
        OurModIds.Clear();
        _nativeOpaqueKeywords = null;
        _nativeOpaqueSearched = false;
        _nativeServerMaterial = null;
        _nativeServerSearched = false;
    }

    internal static void Refresh()
    {
        var cards = Resources.FindObjectsOfTypeAll<ModShopItem>();
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            var config = card.config;
            if (config == null) continue;

            var cardObject = card.gameObject;
            if (cardObject == null) continue;
            if (!cardObject.scene.IsValid()) continue;

            var definition = ServerComponentCatalog.FindByLabel(config.itemName);
            if (definition == null) continue;

            FoldersByModId[card.modID] = definition.Folder;
            LabelsByModId[card.modID] = definition.Label;
            if (card.itemIcon != null)
                IconsByModId[card.modID] = card.itemIcon.sprite;
            ModIdsByFolder[definition.Folder] = card.modID;
            OurModIds.Add(card.modID);

            if (cardObject.activeSelf)
                cardObject.SetActive(false);
        }
    }

    internal static bool TryGetModId(string folder, out int modId, out string reason)
    {
        Refresh();

        if (ModIdsByFolder.TryGetValue(folder, out modId))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"ModLoader did not expose '{folder}'.";
        return false;
    }

    internal static GameObject? GetPrefab(int modId)
    {
        if (!OurModIds.Contains(modId))
            return null;

        var prefab = ModLoader.instance?.GetModPrefab(modId);
        if (prefab != null)
            Normalize(prefab, modId);
        return prefab;
    }

    internal static void NormalizeExisting(UsableObject usable)
    {
        if (usable == null) return;

        if (ModIdsByFolder.Count == 0)
            Refresh();

        string folder = usable.modFolderName;
        if (string.IsNullOrEmpty(folder)) return;
        if (!ModIdsByFolder.TryGetValue(folder, out int modId)) return;

        var prefab = ModLoader.instance?.GetModPrefab(modId) ?? usable.gameObject;
        NormalizeObject(usable, modId, prefab);
    }

    private static void Normalize(GameObject prefab, int modId)
    {
        var usable = prefab.GetComponent<UsableObject>();
        if (usable != null)
            NormalizeObject(usable, modId, prefab);
    }
    internal static bool IsOurComponent(UsableObject usable)
    {
        if (usable == null) return false;
        string folder = usable.modFolderName;
        if (string.IsNullOrEmpty(folder)) return false;

        if (ModIdsByFolder.Count == 0)
            Refresh();

        return ModIdsByFolder.ContainsKey(folder);
    }


    private static void NormalizeObject(UsableObject usable, int modId, GameObject prefab)
    {
        usable.objectInHandType = PlayerManager.ObjectInHand.Router;
        usable.maxInHand = 1;
        if (usable.prefabID <= 0)
            usable.prefabID = modId;

        foreach (var renderer in usable.GetComponentsInChildren<Renderer>(true))
            MakeOpaque(renderer);

        string label = LabelsByModId.TryGetValue(modId, out var knownLabel)
            ? knownLabel
            : prefab.name;

        if (usable.item == null)
        {
            try
            {
                var item = ScriptableObject.CreateInstance<Item>();
                if (item != null)
                {
                    item.itemName = label;
                    item.prefab = prefab;
                    item.price = 0;
                    usable.item = item;
                }
            }
            catch (Exception)
            {
            }
        }

        if (usable.shopItemSO == null)
        {
            try
            {
                var shopItem = ScriptableObject.CreateInstance<ShopItemSO>();
                if (shopItem != null)
                {
                    shopItem.itemName = label;
                    shopItem.itemID = modId;
                    shopItem.itemType = PlayerManager.ObjectInHand.Router;
                    shopItem.price = 0;
                    if (IconsByModId.TryGetValue(modId, out var icon))
                        shopItem.sprite = icon;
                    usable.shopItemSO = shopItem;
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static void MakeOpaque(Renderer renderer)
    {
        if (renderer == null) return;
        try
        {
            var material = renderer.material;
            if (material == null) return;
            renderer.sortingOrder = 0;
            try { renderer.sortingLayerName = "Default"; } catch (Exception) { }


            var serverMaterial = FindNativeServerMaterial();
            if (serverMaterial != null && !material.name.Contains("(Native Server)"))
            {
                var serverClone = CloneServerMaterial(serverMaterial, material);
                renderer.material = serverClone;
                return;
            }

            if (!material.HasProperty("_ZWrite"))
                material = ReplaceWithOpaqueMaterial(renderer, material);

            material.renderQueue = 2000;
            material.SetOverrideTag("RenderType", "Opaque");
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 1);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", 4);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", 1);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", 0);
            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 0f);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", 0f);
            if (material.HasProperty("_BlendMode"))
                material.SetFloat("_BlendMode", 0f);
            if (material.HasProperty("_ZTestTransparent"))
                material.SetFloat("_ZTestTransparent", 4f);
            if (material.HasProperty("_ZTestGBuffer"))
                material.SetFloat("_ZTestGBuffer", 4f);
            if (material.HasProperty("_TransparentZWrite"))
                material.SetFloat("_TransparentZWrite", 1f);
            if (material.HasProperty("_AlphaCutoffEnable"))
                material.SetFloat("_AlphaCutoffEnable", 0f);
            if (material.HasProperty("_CullMode"))
                material.SetFloat("_CullMode", 0f);
            if (material.HasProperty("_CullModeForward"))
                material.SetFloat("_CullModeForward", 0f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_BLENDMODE_ALPHA");
            material.DisableKeyword("_BLENDMODE_PREMULTIPLY");
            material.DisableKeyword("_BLENDMODE_ADD");
            material.DisableKeyword("_BLENDMODE_MULTIPLY");
            material.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
            material.DisableKeyword("_DEPTHOFFSET_ON");
            material.DisableKeyword("_WRITE_TRANSPARENT_MOTION_VECTOR");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            ApplyNativeOpaqueKeywords(material);
        }
        catch (Exception)
        {
        }
    }



    private static Material? _nativeServerMaterial;
    private static bool _nativeServerSearched;
    private static Renderer? _nativeServerRenderer;

    private static Material? FindNativeServerMaterial()
    {
        if (!_nativeServerSearched)
        {
            _nativeServerSearched = true;
            foreach (var server in UnityEngine.Object.FindObjectsOfType<Il2Cpp.Server>())
            {
                foreach (var renderer in server.GetComponentsInChildren<Renderer>(true))
                {
                    var candidate = renderer.sharedMaterial;
                    if (candidate == null || candidate.shader == null) continue;
                    if (candidate.renderQueue < 2000 || candidate.renderQueue >= 3000) continue;
                    _nativeServerMaterial = candidate;
                    _nativeServerRenderer = renderer;
                    break;
                }
                if (_nativeServerMaterial != null) break;
            }
        }

        return _nativeServerMaterial;
    }

    internal static string NativeServerInfo()
    {
        var renderer = _nativeServerRenderer;
        var material = _nativeServerMaterial;
        if (renderer == null)
            return "native server: none found";

        var keywords = material != null ? string.Join(",", material.shaderKeywords) : "<none>";
        return $"native server renderer={renderer.name} layer={renderer.gameObject.layer} " +
               $"bounds={renderer.bounds.size} mat={(material != null ? material.name : "<none>")} " +
               $"shader={(material != null && material.shader != null ? material.shader.name : "<none>")} " +
               $"queue={(material != null ? material.renderQueue : -1)} keywords={keywords}";
    }

    private static Material CloneServerMaterial(Material native, Material source)
    {
        var nativeKeywords = native.shaderKeywords;
        var clone = new Material(native);
        clone.name = source.name + " (Native Server)";

        clone.CopyPropertiesFromMaterial(source);

        var depthProperties = new[]
        {
            "_SurfaceType", "_BlendMode", "_ZWrite", "_ZTest", "_ZTestTransparent",
            "_ZTestGBuffer", "_TransparentZWrite", "_CullMode", "_CullModeForward",
            "_AlphaCutoffEnable", "_Stencil", "_StencilRef", "_StencilWriteMask",
            "_StencilReadMask", "_StencilComp", "_StencilPass", "_StencilFail", "_StencilZFail",
        };
        foreach (var property in depthProperties)
        {
            if (native.HasProperty(property) && clone.HasProperty(property))
                clone.SetFloat(property, native.GetFloat(property));
        }
        if (clone.HasProperty("_CullMode"))
            clone.SetFloat("_CullMode", 0f);
        if (clone.HasProperty("_CullModeForward"))
            clone.SetFloat("_CullModeForward", 0f);

        clone.shaderKeywords = nativeKeywords;
        clone.renderQueue = native.renderQueue;
        return clone;
    }
    private static string[]? _nativeOpaqueKeywords;
    private static bool _nativeOpaqueSearched;

    private static void ApplyNativeOpaqueKeywords(Material material)
    {
        if (!_nativeOpaqueSearched)
        {

            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                var candidate = renderer.sharedMaterial;
                if (candidate == null || candidate.shader == null) continue;
                if (candidate.shader.name != "HDRP/Lit") continue;
                if (!candidate.HasProperty("_SurfaceType")) continue;
                if (candidate.GetFloat("_SurfaceType") > 0.5f) continue;
                if (candidate.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) continue;
                _nativeOpaqueKeywords = candidate.shaderKeywords;
                break;
            }
            _nativeOpaqueSearched = _nativeOpaqueKeywords != null;
        }

        if (_nativeOpaqueKeywords != null)
            material.shaderKeywords = _nativeOpaqueKeywords;
    }

    private static Material ReplaceWithOpaqueMaterial(Renderer renderer, Material source)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("HDRP/Lit");
        if (shader == null) return source;

        var replacement = new Material(shader);
        replacement.name = source.name + " (Opaque)";

        if (source.HasProperty("_Color"))
            replacement.color = source.color;
        else if (source.HasProperty("_BaseColor"))
            replacement.color = source.GetColor("_BaseColor");

        var texture = source.mainTexture;
        if (texture != null)
        {
            if (replacement.HasProperty("_MainTex"))
                replacement.SetTexture("_MainTex", texture);
            if (replacement.HasProperty("_BaseMap"))
                replacement.SetTexture("_BaseMap", texture);
        }

        replacement.renderQueue = 2000;
        replacement.SetOverrideTag("RenderType", "Opaque");
        replacement.SetInt("_ZWrite", 1);
        replacement.SetInt("_ZTest", 4);
        replacement.SetInt("_SrcBlend", 1);
        replacement.SetInt("_DstBlend", 0);
        replacement.SetFloat("_Mode", 0f);
        replacement.SetFloat("_Surface", 0f);
        if (replacement.HasProperty("_SurfaceType"))
            replacement.SetFloat("_SurfaceType", 0f);
        if (replacement.HasProperty("_BlendMode"))
            replacement.SetFloat("_BlendMode", 0f);
        if (replacement.HasProperty("_ZTestTransparent"))
            replacement.SetFloat("_ZTestTransparent", 4f);
        if (replacement.HasProperty("_ZTestGBuffer"))
            replacement.SetFloat("_ZTestGBuffer", 4f);
        if (replacement.HasProperty("_TransparentZWrite"))
            replacement.SetFloat("_TransparentZWrite", 1f);
        replacement.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        replacement.DisableKeyword("_BLENDMODE_ALPHA");
        replacement.DisableKeyword("_BLENDMODE_PREMULTIPLY");
        replacement.DisableKeyword("_BLENDMODE_ADD");
        replacement.DisableKeyword("_BLENDMODE_MULTIPLY");
        ApplyNativeOpaqueKeywords(replacement);
        renderer.material = replacement;
        return replacement;
    }
}
