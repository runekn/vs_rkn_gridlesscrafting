# RKN Generic Crafting

Generic gridless crafting system that completely replaces all UI grid recipes.

## Description

Right click with any non-tool item or block on a solid surface, while holding the crafting button (ALT, rebindable), to create a crafting surface.
Right click with additional items or blocks to add more ingredients. Once all the required ingredients has been added, hold right click with the appropriate tool or hands to craft.

Once crafting has completed, the ingredients will despawn, and the recipe output will spawn.

If there are enough remaining ingredients to continue crafting, then continuing to hold rick click will continue crafting at a slightly faster pace.

If a recipe requires tools, then these must be held in your hands while trying to craft. If a recipe requires two tools then the second one must be held in offhand slot.

Any recipe that has their ingredients (except tools) satisfied by those added to crafting surface, will appear in the top info bar while looking at the crafting surface.
If there are more than one available recipe, then you can switch between them by holding crafting button and right clicking.

The crafting surface can de destroyed to reclaim ingredients.

In order to not functions as a hyper-efficient ground storage, any crafting surface will auto delete itself after 2 minutes of inactivity (configurable).

![Crafting a simple flint knife.](ReadmeAssets/demo1.gif)
![Selecting and crafting multi-tool recipe on much more efficient surface.](ReadmeAssets/demo2.gif)

### Crafting speed

The time it takes to craft is measured as `baseCraftingTimeSeconds * craftingSurfaceTimeModifier * recipeCraftingTimeModifier * Max(ConsecutiveCraftingTimeModifierMin, ConsecutiveCraftingTimeModifier^(amount))`.

Where `baseCraftingTimeSeconds` is configurable, `craftingSurfaceTimeModifier` depends on what block is under the crafting surface, `recipeCraftingTimeModifier` depends on the recipe, and `consecutiveModifer` decreases after each consecutive crafting (with minimnum cap).

As an example for `craftingSurfaceTimeModifier`: tables provides the fastest crafting speed, while cobble stone is a bit slower, and soil is the slowest.

For `recipeCraftingTimeModifier` the only recipes currently that modifies this are tool and weapon recipes.

### Adjusted recipes

In order to work with the mod, some recipes had to be adjusted. These are recipes that in vanilla require more than two tools, or combination of tools that did not fit the animation set.

* All figureheads have reduced required tools from four to two.
* Many mechanical parts have reduced required tools to two.
* Cabinets no longer require hammer.
* Grind stone wheel no longer require hammer.
* Plank blocks, slabs, and stairs now requires hammer. Just because I wanted more recipes to have tool requirements now that it plays animations. Though I disabled durability cost at least.

## Config

File: `%AppData%/Roaming/VintagestoryData/ModConfig/rkncrafting.json`

| Key   | Default | Description | Authorative side |
|-------|---------|------------|-------------------|
| BaseCraftingTimeSeconds | 1.0 | Base seconds to craft. | Server |
| AutoDeleteTimeSeconds | 120 | How many seconds of inactivity it takes for crafting surface to self-delete. | Server |
| ConsecutiveCraftingTimeModifier | 0.95 | Amount to decrease time to craft while continuing to hold right click. | Server |
| ConsecutiveCraftingTimeModifierMin | 0.5 | The minimum amount that crafting time can be decreased during consecutive crafting. | Server |
| EnableBulkCrafting | false | Allows for holding SHIFT while crafting to craft as many items as possible at once.| Server |
| BulkBaseCraftingTimeSeconds | 2.0 | Base seconds to craft if using bulk crafting.| Server |
| DisableUICraftingGrid | true | Disables the inventory UI crafting grid. | Client |

## Required dependencies

* [JSON Patches lib](https://mods.vintagestory.at/jsonpatcheslib)

## Compatibility

Fully safe to add to existing save. It is recommended that all crafting surfaces are deleted before removing from existing save. Though given the auto-delete feature this should be trivial.

Any mod that disables grid recipes will also disable for this mod.

## Wishlist

* Some better way of selecting recipe in case of multiple
* More and better animations based on tools and recipe
* Animations for crafted item or block taking shape while crafting
	* Estimated difficulty: VERY HARD (though can be done one recipie at a time)
* Particles while crafting
	* Estimated difficulty: MEDIUM
* Particles per item type
	* Estimated difficulty: MEDIUM-HARD (though can be done one recipie at a time)
* Sounds while crafting
	* Estimated difficulty: MEDIUM
* Sounds per item type
	* Estimated difficulty: MEDIUM-HARD (though can be done one recipie at a time)
* Grid
	* Add selection boxes at each of the 9 ingredient slots and require grid based recipes like vanilla, just in-world.
	* Minus tool slots of course
	* Estimated difficulty: HARD
* Pressing H while looking at crafting surface opens selected recipe output
	* Estimated difficulty: MEDIUM
* Support adding ingredient while in mouse mode
	* Estimated difficulty: MEDIUM
* Support >2 tool recipes through partial craft items.
	* When crafting recipe that requires >2 tools, it will output a partial craft item instead of the recipe output. This then needs to be put into a new crafting surface where it can then be worked on with the remaining tools.
	* Estimated difficulty: HARD

## Known issues

* Other players in multiplayer do not see the same ingredients, or with the same size, unless they add to it.
* Some recipes have the same name and are indistinguishable in the recipe selection.

## Changelog

* 0.1.0: Initial pre-release
