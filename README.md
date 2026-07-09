# RKN Gridless Crafting

Generic gridless crafting system that completely replaces all UI grid recipes.

## Description

Right click with any non-tool item or block on a solid surface, while holding the crafting button (ALT, rebindable), to create a crafting surface.
Right click with additional items or blocks to add more ingredients. Once all the required ingredients has been added, hold right click with the appropriate tool or hands to craft.

Once crafting has completed, the ingredients will despawn, and the recipe output will spawned.

If there are enough remaining ingredients to continue crafting, then continuing to hold rick click will continue crafting at a slightly faster pace.

If a recipe requires tools, then these must be held in your hands while trying to craft. If a recipe requires two tools then the second one must be held in offhand slot.

Any recipe that has their ingredients (except tools) satisfied by the those added to crafting surface, will appear in the top info bar while looking at the crafting surface.
If there are more than one available recipe, then you can switch between them by holding crafting button and right clicking.

The crafting surface can de destroyed to reclaim ingredients.

In order to not functions as a hyper-efficient ground storage, any crafting surface will auto delete itself after 2 minutes of inactivity (configurable).

## Crafting speed

The time it takes to craft is measured as `baseCraftingTimeSeconds * craftingSurfaceTimeModifier * recipeCraftingTimeModifier * Max(ConsecutiveCraftingTimeModifierMin, ConsecutiveCraftingTimeModifier^(amount))`.

Where `baseCraftingTimeSeconds` is configurable, `craftingSurfaceTimeModifier` depends on what block is under the crafting surface, `recipeCraftingTimeModifier` depends on the recipe, and `consecutiveModifer` decreases after each consecutive crafting (with minimnum cap).

As an example for `craftingSurfaceTimeModifier`: tables provides the fastest crafting speed, while cobble stone is a bit slower, and soil is the slowest.

For `recipeCraftingTimeModifier` the only recipes currently that modifies this are tool and weapon recipies.

## Adjusted recipes

In order to work with the mod, some recipes had to be adjusted. These are recipies that in vanilla require more than two tools, or combination of tools that did not fit the animation set.

* All figureheads have reduced required tools from four to two.
* Many mechanical parts have reduced required tools to two.
* Cabinets no longer require hammer.
* Grind stone wheel no longer require hammer.
* Plank blocks, slabs, and stairs now requires hammer. Just because I wanted more recipes to have tool requirements now that it plays animations. Though I disabled durability cost at least.

## Config

| Key   | Default | Description |
|-------|---------|------------|
| BaseCraftingTimeSeconds | 1.0 | Base seconds to craft. |
| AutoDeleteTimeSeconds | 120 | How many seconds of inactivity it takes for crafting surface to self-delete. |
| ConsecutiveCraftingTimeModifier | 0.95 | Amount to decrease time to craft while continuing to hold right click. |
| ConsecutiveCraftingTimeModifierMin | 0.5 | The minimum amount that crafting time can be decreased during consecutive crafting. |
| EnableBulkCrafting | false | Allows for holding SHIFT while crafting to craft as many items as possible at once.|
| BulkBaseCraftingTimeSeconds | 2.0 | Base seconds to craft if using bulk crafting.|
| DisableUICraftingGrid | true | Disabled the inventory UI crafting grid. |

## Changelog

* 0.1.0: Initial pre-release
