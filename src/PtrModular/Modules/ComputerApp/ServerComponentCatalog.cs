using System;
using Il2Cpp;
using UnityEngine;

namespace PtrModular.Modules;

internal sealed class ServerComponent
{
    internal ServerComponent(
        string folder,
        string label,
        int price,
        PlayerManager.ObjectInHand itemType,
        Vector3 colliderCenter,
        Vector3 colliderSize)
    {
        Folder = folder;
        Label = label;
        Price = price;
        ItemType = itemType;
        ColliderCenter = colliderCenter;
        ColliderSize = colliderSize;
    }

    internal string Folder { get; }
    internal string Label { get; }
    internal int Price { get; }
    internal PlayerManager.ObjectInHand ItemType { get; }
    internal Vector3 ColliderCenter { get; }
    internal Vector3 ColliderSize { get; }
}

internal static class ServerComponentCatalog
{
    internal static readonly ServerComponent[] All =
    {
        new ServerComponent(
            "ptr_modular_server_case", "Server Case", 0, PlayerManager.ObjectInHand.Router,
            new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.5f)),
        new ServerComponent(
            "ptr_modular_motherboard", "Motherboard", 0, PlayerManager.ObjectInHand.Router,
            new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.02f, 0.35f)),
        new ServerComponent(
            "ptr_modular_cpu", "CPU", 0, PlayerManager.ObjectInHand.Router,
            new Vector3(0f, 0.01f, 0f), new Vector3(0.06f, 0.02f, 0.06f)),
        new ServerComponent(
            "ptr_modular_gpu", "GPU", 0, PlayerManager.ObjectInHand.Router,
            new Vector3(0f, 0.025f, 0f), new Vector3(0.3f, 0.05f, 0.12f)),
    };

    internal static ServerComponent? FindByLabel(string label)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i].Label, label, StringComparison.Ordinal))
                return All[i];
        }

        return null;
    }
}
