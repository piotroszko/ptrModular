using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PtrModular.Modules;

[HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.GetPrefabForItem))]
internal static class ServerComponentPrefabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        int __0,
        PlayerManager.ObjectInHand __1,
        ref GameObject __result)
    {
        var prefab = ServerComponentRegistry.GetPrefab(__0);
        if (prefab == null) return true;

        __result = prefab;
        return false;
    }
}

[HarmonyPatch(typeof(UsableObject), nameof(UsableObject.MoveToHand))]
internal static class ModItemPickupPatch
{
    [HarmonyPostfix]
    private static void Postfix(UsableObject __instance)
    {
        if (!ServerComponentRegistry.IsOurComponent(__instance)) return;

        __instance.maxInHand = 1;
        __instance.objectInHands = true;

        var player = PlayerManager.instance;
        if (player != null)
            player.objectInHand = __instance.objectInHandType;
    }
}
