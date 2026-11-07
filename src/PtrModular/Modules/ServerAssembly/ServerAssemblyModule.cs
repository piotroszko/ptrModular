using System;
using System.Collections.Generic;
using Il2Cpp;
using PtrLib.Items;
using PtrLib.Mod;
using PtrLib.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PtrModular.Modules;

internal sealed class ServerAssemblyModule : PtrModule
{
    private const string CaseFolder = "ptr_modular_server_case";
    private const float OpenDistance = 4f;
    private const float SlotTolerance = 0.4f;
    private const float ScanInterval = 0.2f;

    private sealed class AssemblyState
    {
        internal AssemblyState(GameObject caseObject)
        {
            Case = caseObject;
        }

        internal GameObject Case { get; }
        internal Dictionary<string, UsableObject> Installed { get; } =
            new Dictionary<string, UsableObject>();
        internal bool Complete { get; set; }
        internal bool PhysicsCaptured { get; set; }
        internal bool WasKinematic { get; set; }
        internal bool HadGravity { get; set; }
        internal List<GameObject>? Panels { get; set; }
    }

    private readonly Dictionary<int, AssemblyState> _assemblies =
        new Dictionary<int, AssemblyState>();
    private readonly HashSet<int> _wasHeld = new HashSet<int>();

    private AssemblyState? _open;
    private UsableObject? _hovered;
    private AssemblyState? _near;
    private float _nextScan;
    private float _nextVisualScan;
    private GUIStyle? _labelStyle;

    private readonly Dictionary<int, Color[]> _highlightColors =
        new Dictionary<int, Color[]>();

    public override string Id => "ServerAssembly";
    public override string DisplayName => "Server Assembly";
    public override bool EnabledByDefault => true;

    public override void OnGameplayEnter()
    {
        _open = null;
        _near = null;
        _assemblies.Clear();
        _hovered = null;
        _highlightColors.Clear();
        _wasHeld.Clear();
        _nextScan = 0f;
        _nextVisualScan = 0f;
    }

    public override void OnUpdate()
    {
        if (!Context.InGameplay) return;

        var player = PlayerManager.instance;
        if (player == null || player.playerGO == null) return;

        if (Time.unscaledTime >= _nextVisualScan)
        {
            _nextVisualScan = Time.unscaledTime + ScanInterval;
            UpdateCaseVisuals();
        }

        bool toggle = TogglePressed();
        if (_open != null)
        {
            if (_open.Case == null || !_open.Case.activeInHierarchy)
            {
                _open = null;
                return;
            }


            UpdatePickedUp(_open);
            UpdateHover(_open, player);
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                PickupHovered(_open);

            var remove = RemovalKeyPressed();
            if (remove != null)
                RemoveInstalled(_open, remove);
            if (toggle)
            {
                CloseAssembly(_open);
                return;
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            UpdatePlacement(_open);
            return;
        }

        var aimed = FindAimedCase(player);
        _near = aimed;
        if (aimed != null && toggle)
            OpenAssembly(aimed);
    }

    public override void OnGui()
    {
        if (!Context.InGameplay) return;

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle
            {
                font = GUI.skin.label.font,
                fontSize = 18,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            _labelStyle.normal.textColor = Color.white;
        }

        if (_open == null)
        {
            if (_near != null)
            {
                var prompt = new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.72f, 420f, 54f);
                GUI.Box(prompt, "Server Rack");
                GUI.Label(
                    new Rect(prompt.x + 14f, prompt.y + 26f, prompt.width - 28f, 24f),
                    "Press F to open the assembly rack.",
                    _labelStyle);
            }

            return;
        }

        var rect = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.55f, 480f, 280f);
        GUI.Box(rect, "Server Rack Assembly");

        string State(string folder, string label, string key)
        {
            return _open.Installed.ContainsKey(folder)
                ? $"{key}  {label}: installed"
                : $"{key}  {label}: empty";
        }

        var body =
            "Carry each part to its slot, then drop it within the slot area.\n" +
            "Look at an installed part and click to remove it. F1/F2/F3 also remove; F closes.\n\n" +
            State("ptr_modular_motherboard", "Motherboard", "F1") + "\n" +
            State("ptr_modular_cpu", "CPU", "F2") + "\n" +
            State("ptr_modular_gpu", "GPU", "F3") + "\n\n" +
            (_open.Complete
                ? "Assembly complete. Panels are closed."
                : $"Installed {_open.Installed.Count}/3.");

        GUI.Label(
            new Rect(rect.x + 16f, rect.y + 32f, rect.width - 32f, rect.height - 45f),
            body,
            _labelStyle);
    }
    private void UpdateCaseVisuals()
    {
        var usableObjects = UnityEngine.Object.FindObjectsOfType<UsableObject>();
        for (int i = 0; i < usableObjects.Length; i++)
        {
            var usable = usableObjects[i];
            if (usable == null || usable.gameObject == null) continue;
            if (!string.Equals(usable.modFolderName, CaseFolder, StringComparison.Ordinal))
                continue;

            var state = GetOrCreateState(usable.gameObject);
            EnsurePanels(state);
            if (!ReferenceEquals(state, _open))
                SetPanels(state, true);
        }
    }


    private AssemblyState? FindAimedCase(PlayerManager player)
    {
        Transform? view = player.vcam != null ? player.vcam.transform : null;
        if (view == null && Camera.main != null)
            view = Camera.main.transform;
        if (view == null) return null;

        if (!Physics.Raycast(
                view.position,
                view.forward,
                out var hit,
                OpenDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
            return null;

        var usable = hit.collider != null
            ? hit.collider.GetComponentInParent<UsableObject>()
            : null;
        if (usable == null || usable.gameObject == null) return null;
        if (!string.Equals(usable.modFolderName, CaseFolder, StringComparison.Ordinal))
            return null;
        if (usable.objectInHands) return null;

        ModItemRegistry.NormalizeExisting(usable);
        return GetOrCreateState(usable.gameObject);
    }

    private AssemblyState GetOrCreateState(GameObject caseObject)
    {
        int id = caseObject.GetInstanceID();
        if (_assemblies.TryGetValue(id, out var state))
            return state;

        state = new AssemblyState(caseObject);
        _assemblies[id] = state;
        return state;
    }

    private void UpdatePlacement(AssemblyState state)
    {
        if (state.Case == null)
        {
            _open = null;
            return;
        }

        var usableObjects = UnityEngine.Object.FindObjectsOfType<UsableObject>();
        for (int i = 0; i < usableObjects.Length; i++)
        {
            var usable = usableObjects[i];
            if (usable == null || usable.gameObject == null) continue;
            ModItemRegistry.NormalizeExisting(usable);
            if (usable.gameObject == state.Case) continue;

            string folder = usable.modFolderName;
            if (!TryGetSlot(folder, out var localPosition, out var localEuler, out float scale))
                continue;
            if (state.Installed.ContainsKey(folder)) continue;

            int id = usable.gameObject.GetInstanceID();
            if (usable.objectInHands)
            {
                _wasHeld.Add(id);
                continue;
            }

            if (!_wasHeld.Contains(id)) continue;

            var target = state.Case.transform.TransformPoint(localPosition);
            if (Vector3.Distance(usable.transform.position, target) > SlotTolerance)
                continue;

            Install(state, usable, folder, localPosition, localEuler, scale);
            _wasHeld.Remove(id);
        }

        if (!state.Complete && state.Installed.Count >= 3)
        {
            state.Complete = true;
            state.Case.name = "Assembled Server";
            ClearHighlight();
            SetInstalledInteraction(state, false);
            RestoreCasePhysics(state);
            SetCaseCollider(state, true);
            SetPanels(state, true);
            _open = null;
            Context.Log.Msg("Server assembly complete; physical case retained.");
        }
    }

    private void Install(
        AssemblyState state,
        UsableObject usable,
        string folder,
        Vector3 localPosition,
        Vector3 localEuler,
        float scale)
    {
        var part = usable.gameObject;
        usable.objectInHands = false;
        part.transform.SetParent(state.Case.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(localEuler);
        part.transform.localScale = Vector3.one * scale;

        foreach (var collider in part.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        var body = part.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        try
        {
            CoopLooseItems.Unregister(usable);
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Could not unregister installed part: {exception.Message}");
        }

        state.Installed[folder] = usable;
        Context.Log.Msg(
            $"Installed {folder} at its server slot " +
            $"({state.Installed.Count}/3).");
    }

    private void OpenAssembly(AssemblyState state)
    {
        CaptureCasePhysics(state);
        EnsurePanels(state);
        SetPanels(state, false);
        SetCaseCollider(state, false);
        SetCaseFrozen(state, true);
        _open = state;
        Context.Log.Msg($"Opened server assembly at {state.Case.transform.position}.");
        SetInstalledInteraction(state, true);
    }

    private void CloseAssembly(AssemblyState state)
    {
        SetCaseCollider(state, true);
        SetPanels(state, true);
        RestoreCasePhysics(state);
        ClearHighlight();
        SetInstalledInteraction(state, false);
        _open = null;
        Context.Log.Msg("Closed server assembly.");
    }

    private static void CaptureCasePhysics(AssemblyState state)
    {
        if (state.Case == null || state.PhysicsCaptured) return;
        var body = state.Case.GetComponent<Rigidbody>();
        if (body == null) return;

        state.PhysicsCaptured = true;
        state.WasKinematic = body.isKinematic;
        state.HadGravity = body.useGravity;
    }

    private static void SetCaseFrozen(AssemblyState state, bool frozen)
    {
        if (state.Case == null) return;
        var body = state.Case.GetComponent<Rigidbody>();
        if (body == null) return;

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = frozen;
        body.useGravity = !frozen;
    }

    private static void RestoreCasePhysics(AssemblyState state)
    {
        if (state.Case == null || !state.PhysicsCaptured) return;
        var body = state.Case.GetComponent<Rigidbody>();
        if (body == null) return;

        body.isKinematic = state.WasKinematic;
        body.useGravity = state.HadGravity;
        state.PhysicsCaptured = false;
    }


    private static void EnsurePanels(AssemblyState state)
    {
        if (state.Panels != null || state.Case == null) return;

        var source = state.Case.GetComponentInChildren<Renderer>();
        var material = source != null ? source.sharedMaterial : null;
        EnsureDepth(material);
        var panels = new List<GameObject>();

        CreatePanel(state, panels, "PtrModular Case Side",
            new Vector3(0.235f, 0.13f, 0f),
            new Vector3(0.012f, 0.26f, 0.70f), material);
        CreatePanel(state, panels, "PtrModular Case Top",
            new Vector3(0f, 0.252f, 0f),
            new Vector3(0.48f, 0.012f, 0.70f), material);
        CreatePanel(state, panels, "PtrModular Case Front",
            new Vector3(0f, 0.13f, 0.352f),
            new Vector3(0.48f, 0.26f, 0.012f), material);

        state.Panels = panels;
    }

    private static void EnsureDepth(Material? material)
    {
        if (material == null) return;
        try
        {
            material.renderQueue = 2000;
            material.SetInt("_ZWrite", 1);
            material.SetInt("_ZTest", 4);
        }
        catch (Exception)
        {
        }
    }

    private static void CreatePanel(
        AssemblyState state,
        List<GameObject> panels,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material? material)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;

        var collider = panel.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);

        panel.transform.SetParent(state.Case.transform, false);
        panel.transform.localPosition = localPosition;
        panel.transform.localScale = localScale;

        var renderer = panel.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (material != null)
            {
                renderer.sharedMaterial = material;
                EnsureDepth(renderer.material);
            }
            else
            {
                var panelMaterial = renderer.material;
                panelMaterial.color = new Color(0.23f, 0.25f, 0.28f);
                if (panelMaterial.HasProperty("_BaseColor"))
                    panelMaterial.SetColor("_BaseColor", new Color(0.23f, 0.25f, 0.28f));
                EnsureDepth(panelMaterial);
            }
        }

        panels.Add(panel);
    }

    private static void SetPanels(AssemblyState state, bool closed)
    {
        EnsurePanels(state);
        if (state.Panels == null) return;

        for (int i = 0; i < state.Panels.Count; i++)
        {
            if (state.Panels[i] != null)
                state.Panels[i].SetActive(closed);
        }
    }

    private void RemoveInstalled(AssemblyState state, string folder)
    {
        if (state.Case == null) return;
        if (!state.Installed.TryGetValue(folder, out var usable)) return;
        if (usable == null || usable.gameObject == null)
        {
            state.Installed.Remove(folder);
            return;
        }

        state.Installed.Remove(folder);
        var part = usable.gameObject;
        var caseTransform = state.Case.transform;

        part.transform.SetParent(null, true);
        part.transform.position = caseTransform.position
            + caseTransform.right * 0.55f
            + Vector3.up * 0.45f;
        part.transform.rotation = Quaternion.identity;

        foreach (var collider in part.GetComponentsInChildren<Collider>(true))
            collider.enabled = true;

        var body = part.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        usable.objectInHands = false;
        try
        {
            CoopLooseItems.Register(usable);
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Could not register removed part: {exception.Message}");
        }

        state.Complete = false;
        state.Case.name = "Server Case";
        SetPanels(state, false);
        Context.Log.Msg($"Removed {folder} from server case.");
    }

    private static string? RemovalKeyPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return null;
        if (keyboard.f1Key.wasPressedThisFrame) return "ptr_modular_motherboard";
        if (keyboard.f2Key.wasPressedThisFrame) return "ptr_modular_cpu";
        if (keyboard.f3Key.wasPressedThisFrame) return "ptr_modular_gpu";
        return null;
    }

    private static void SetInstalledInteraction(AssemblyState state, bool enabled)
    {
        foreach (var usable in state.Installed.Values)
        {
            if (usable == null) continue;
            foreach (var collider in usable.GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }
    }

    private void UpdatePickedUp(AssemblyState state)
    {
        List<string>? picked = null;
        foreach (var pair in state.Installed)
        {
            if (pair.Value != null && pair.Value.objectInHands)
                (picked ??= new List<string>()).Add(pair.Key);
        }

        if (picked == null) return;
        for (int i = 0; i < picked.Count; i++)
        {
            state.Installed.Remove(picked[i]);
            state.Complete = false;
            state.Case.name = "Server Case";
            SetPanels(state, false);
            Context.Log.Msg($"Picked up {picked[i]} from server case.");
        }
    }
    private void PickupHovered(AssemblyState state)
    {
        var part = _hovered;
        if (part == null || part.gameObject == null) return;

        string? folder = null;
        foreach (var pair in state.Installed)
        {
            if (ReferenceEquals(pair.Value, part))
            {
                folder = pair.Key;
                break;
            }
        }

        if (folder == null) return;

        state.Installed.Remove(folder);
        state.Complete = false;
        state.Case.name = "Server Case";
        SetPanels(state, false);
        ClearHighlight();

        try
        {
            if (!part.objectInHands)
                part.MoveToHand();
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Could not pick up {folder}: {exception.Message}");
        }

        Context.Log.Msg($"Picked up {folder} from server case by click.");
    }


    private void UpdateHover(AssemblyState state, PlayerManager player)
    {
        var part = RaycastInstalled(state, player);
        if (ReferenceEquals(part, _hovered)) return;

        if (_hovered != null)
            SetHighlight(_hovered, false);

        _hovered = part;
        if (_hovered != null)
            SetHighlight(_hovered, true);
    }

    private static UsableObject? RaycastInstalled(AssemblyState state, PlayerManager player)
    {
        Transform? view = player.vcam != null ? player.vcam.transform : null;
        if (view == null && Camera.main != null)
            view = Camera.main.transform;
        if (view == null) return null;

        if (!Physics.Raycast(
                view.position,
                view.forward,
                out var hit,
                OpenDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
            return null;

        var usable = hit.collider != null
            ? hit.collider.GetComponentInParent<UsableObject>()
            : null;
        if (usable == null) return null;

        foreach (var pair in state.Installed)
        {
            if (ReferenceEquals(pair.Value, usable))
                return usable;
        }

        return null;
    }

    private void SetHighlight(UsableObject usable, bool enabled)
    {
        int id = usable.GetInstanceID();

        try
        {
            var outline = usable.outlineEffect;
            if (outline != null)
            {
                outline.enabled = enabled;
                if (enabled) return;
            }
        }
        catch (Exception)
        {
        }

        var renderers = usable.GetComponentsInChildren<Renderer>(true);
        if (enabled)
        {
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i].material.color;
                renderers[i].material.color = new Color(1f, 0.65f, 0.1f);
            }

            _highlightColors[id] = colors;
        }
        else if (_highlightColors.TryGetValue(id, out var saved))
        {
            for (int i = 0; i < renderers.Length && i < saved.Length; i++)
                renderers[i].material.color = saved[i];
            _highlightColors.Remove(id);
        }
    }

    private void ClearHighlight()
    {
        if (_hovered != null)
            SetHighlight(_hovered, false);
        _hovered = null;
    }

    private static void SetCaseCollider(AssemblyState state, bool enabled)
    {
        if (state.Case == null) return;
        var colliders = state.Case.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }
    private static bool TogglePressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.fKey.wasPressedThisFrame;
    }

    private static bool TryGetSlot(
        string folder,
        out Vector3 localPosition,
        out Vector3 localEuler,
        out float scale)
    {
        switch (folder)
        {
            case "ptr_modular_motherboard":
                localPosition = new Vector3(0f, 0.045f, 0.02f);
                localEuler = Vector3.zero;
                scale = 0.8f;
                return true;

            case "ptr_modular_cpu":
                localPosition = new Vector3(0f, 0.075f, 0.05f);
                localEuler = Vector3.zero;
                scale = 0.8f;
                return true;

            case "ptr_modular_gpu":
                localPosition = new Vector3(0f, 0.12f, -0.06f);
                localEuler = Vector3.zero;
                scale = 0.8f;
                return true;

            default:
                localPosition = Vector3.zero;
                localEuler = Vector3.zero;
                scale = 1f;
                return false;
        }
    }
}
