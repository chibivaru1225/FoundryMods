# NoLimitShipping

FOUNDRY mod that removes shipping restrictions and expands station storage capacity.

## Features

- **All items shippable** — Every non-hidden item can be sent to the space station via shipping pad, including Science Packs and Construction Sets that are normally non-tradeable.
- **Station storage capacity ×10000** — All item category storage capacities are multiplied by 10000, so ships will actually come for items even at high production volumes.
- **Full station storage visibility** — All shippable items appear in the station storage view, even items with no original trade category.

## How it works

The mod runs three operations just before the native game layer registers item data (`NativeWrapper.registerNativeLibData`):

1. Sets `canBeTradedOnStation = true` and ensures valid prices/weights on all non-hidden items.
2. Multiplies every `ItemCategoryTemplate.stationCapacityPerLevel` by 10000.
3. Assigns a fallback category to items with no category (Science Packs, Construction Sets, etc.) and adds them to the category's item list so they appear as slots in the station storage UI.

Research-unlock visibility gates for the shipping pad and station storage view are bypassed via Harmony patches.

## Installation

Build via FOUNDRY Mod Kit in Unity Editor, then copy the output to your `FOUNDRY/Mods/` folder.
