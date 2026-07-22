using HarmonyLib;
using Vintagestory.API.Common;

namespace RknCrafting.Patches;

[HarmonyPatch(typeof(CraftingRecipeIngredient), "AddToFastSearchRecipes")]
public class CraftingRecipeIngredientPatch
{
    static bool Prefix(IWorldAccessor world, IRecipeBase recipe, ref int __result)
    {
        if (recipe == null)
        {
            __result = -1;
            return false;
        }
        return true;
    }
}