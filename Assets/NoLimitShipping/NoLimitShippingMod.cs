using HarmonyLib;
using System.Collections.Generic;
using System.Text;

public static class NoLimitShippingMod
{
    internal static void SetAllItemsTradeable()
    {
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            ItemTemplate item = kvp.Value;
            if (item.isHiddenItem) continue;

            item.canBeTradedOnStation = true;

            if (item.market_sellPrice <= 0) item.market_sellPrice = 1;
            if (item.market_buyPrice  <= 0) item.market_buyPrice  = 1;

            if (item._baseWeightInGrams < 1000)
                item._baseWeightInGrams = 1000;
        }
    }

    internal static void BoostStationCapacity()
    {
        var seen = new HashSet<ItemCategoryTemplate>();
        ItemCategoryTemplate fallback = null;

        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var cat = kvp.Value.itemCategory;
            if (cat != null && seen.Add(cat))
            {
                cat.stationCapacityPerLevel *= 10000;
                if (fallback == null) fallback = cat;
            }
        }

        UnityEngine.Debug.Log($"[NoLimitShipping] BoostStationCapacity: {seen.Count} categories x10000, fallback={fallback?.name ?? "NULL"}");

        if (fallback == null) return;

        int assigned = 0;
        var nocat = new StringBuilder();
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            ItemTemplate item = kvp.Value;
            if (!item.isHiddenItem && item.itemCategory == null)
            {
                item.itemCategory = fallback;
                fallback.list_itemTemplates.Add(item);
                assigned++;
                if (nocat.Length < 600) nocat.Append(item.identifier).Append(' ');
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] BoostStationCapacity: assigned fallback to {assigned} items: {nocat}");
    }
}

// Called just before native layer reads item template data — correct timing for both
// SetAllItemsTradeable and BoostStationCapacity. Guard against double execution.
[HarmonyPatch(typeof(NativeWrapper), "registerNativeLibData")]
public static class Patch_NativeWrapper_RegisterNativeLibData
{
    static bool _ran = false;
    static void Prefix()
    {
        if (_ran) return;
        _ran = true;
        UnityEngine.Debug.Log("[NoLimitShipping] registerNativeLibData Prefix fired");
        try { NoLimitShippingMod.SetAllItemsTradeable(); }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[NoLimitShipping] SetAllItemsTradeable failed: " + e); }
        try { NoLimitShippingMod.BoostStationCapacity(); }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[NoLimitShipping] BoostStationCapacity failed: " + e); }
    }
}

// ShippingPadConfigFrame.Awake() builds its slot dictionary once and caches it.
[HarmonyPatch(typeof(ShippingPadConfigFrame), "Awake")]
public static class Patch_ShippingPadConfigFrame_Awake
{
    static void Prefix()
    {
        NoLimitShippingMod.SetAllItemsTradeable();
    }
}

// Bypass the research-unlock visibility gate for the shipping pad.
[HarmonyPatch(typeof(ResearchSystem), "isItemVisibleOnShippingPad")]
public static class Patch_ResearchSystem_IsItemVisibleOnShippingPad
{
    static bool Prefix(ulong itemTemplateId, ref bool __result)
    {
        ItemTemplate template = ItemTemplateManager.getItemTemplate(itemTemplateId);
        if (template != null && template.canBeTradedOnStation)
        {
            __result = true;
            return false;
        }
        return true;
    }
}

// Bypass the research-unlock visibility gate for the sky platform storage view.
[HarmonyPatch(typeof(ResearchSystem), "isItemVisibleOnSkyPlatform")]
public static class Patch_ResearchSystem_IsItemVisibleOnSkyPlatform
{
    static bool Prefix(ulong itemTemplateId, ref bool __result)
    {
        ItemTemplate template = ItemTemplateManager.getItemTemplate(itemTemplateId);
        if (template != null && template.canBeTradedOnStation)
        {
            __result = true;
            return false;
        }
        return true;
    }
}
