# FOUNDRY Mod Kit — NoLimitShipping

## Project layout

```
Assets/NoLimitShipping/
  NoLimitShippingMod.cs   — all mod logic (single file)
  README.md
```

Game DLL for reference/decompilation:
`Packages/com.channel3.foundrymodkit/GameAssemblies/Main.dll`

Built mod output (not tracked):
`D:/SteamLibrary/steamapps/common/FOUNDRY/Mods/NoLimitShipping/`

Game log:
`C:/Users/chibivaru/AppData/LocalLow/Channel 3 Entertainment/Foundry/Player.log`

## Architecture notes

### Why OnAllAssetsPostProcessed() does not fire
`C3.AssetProcessor.OnAllAssetsPostProcessed()` is only called for mods that have an asset bundle. This mod has no Unity assets, so no bundle is built, and the callback is skipped entirely. All setup logic runs in a Harmony Prefix on `NativeWrapper.registerNativeLibData` instead — this fires during game session load (step 2/20) before the native layer reads any item data.

### Why uncategorized items need list_itemTemplates
`SkyPlatformStorageFrame.Init()` builds UI slots by iterating each `ItemCategoryTemplate.list_itemTemplates`. Setting `item.itemCategory = fallback` updates the back-reference (item → category) but NOT the forward list (category → items). Items never added to `list_itemTemplates` get no UI slot and are invisible in the station storage, regardless of `isItemVisibleOnSkyPlatform`. The fix is to call `fallback.list_itemTemplates.Add(item)` alongside the category assignment.

### Harmony patch ordering (alphabetical)
HarmonyLib applies patches alphabetically by class name. If a patch class fails (e.g. target method not found), later classes are skipped but earlier ones remain applied. Keep this in mind when adding new patch classes.

### isItemVisibleOnSkyPlatform internals
The original implementation checks `ResearchSystem.singleton.hs_skyPlatform_visibleItemTemplateIds` (a HashSet populated by research unlocks). Items not in this set are hidden. Our Prefix bypasses it by returning true for any item with `canBeTradedOnStation == true`.

## Key types (from Main.dll)

| Type | Relevant members |
|------|-----------------|
| `ItemTemplate` | `canBeTradedOnStation`, `itemCategory`, `market_buyPrice`, `market_sellPrice`, `_baseWeightInGrams`, `isHiddenItem`, `flags` |
| `ItemCategoryTemplate` | `stationCapacityPerLevel`, `list_itemTemplates` |
| `NativeWrapper` | `registerNativeLibData()` — main hook point |
| `SkyPlatformManager` | `getStorageCapacity(ItemTemplate)` — returns `itemCategory.stationCapacityPerLevel * storageLevel` |
| `ResearchSystem` | `isItemVisibleOnShippingPad(ulong)`, `isItemVisibleOnSkyPlatform(ulong)` |
| `ShippingPadConfigFrame` | `Awake()` — rebuilds slot dict; re-run SetAllItemsTradeable here as safety net |
