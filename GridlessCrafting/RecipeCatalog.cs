using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RKN.GridlessCrafting;

public class RecipeCatalog
{
    private static List<GridRecipe> catalog;
    private static ICoreAPI api;

    public static void Initialize(ICoreAPI api)
    {
        //catalog = api.World.GridRecipes.Select(r => { r = r.Clone(); r.Shapeless = true; return r; }).ToList();
        catalog = [.. api.World.GridRecipes]; // TODO: dont need this if you're not gonna map the recipes somehow
        RecipeCatalog.api = api;
    }

    public static void Shutdown()
    {
        api = null;
        catalog = null;
    }

    public static bool IsInitialized()
    {
        return catalog != null;
    }

    public static int GetRecipe(GridRecipe recipe)
    {
        return catalog.FindIndex(r => r == recipe);
    }

    public static GridRecipe GetRecipeById(int id)
    {
        return catalog[id];
    }

    public static List<int> GetValidRecipesWithoutTools(List<ItemSlot> items)
    {
        List<int> result = [];
        for (int i = 0; i < catalog.Count; i++)
        {
            if (MatchesRecipe(items, null, null, catalog[i], true))
            {
                result.Add(i);
            }
        }
        return result;
    }

    public static bool MatchesRecipe(List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, int recipeId)
    {
        return MatchesRecipe(items, primaryTool, offhandTool, catalog[recipeId], false);
    }

    private static bool MatchesRecipe(List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, GridRecipe recipe, bool ignoreTools)
    {
        if (!recipe.Enabled || recipe.ResolvedIngredients == null)
        {
            return false;
        }
        IEnumerable<ItemStack> clonedItems = items.Select(i => i.Itemstack.Clone()).ToList();
        ISet<ItemStack> unusedItems = clonedItems.ToHashSet();
        foreach (CraftingRecipeIngredient? ingredient in recipe.ResolvedIngredients)
        {
            if (ingredient == null)
            {
                continue;
            }
            if (!MatchesIngredient(clonedItems, primaryTool, offhandTool, ingredient, ignoreTools, unusedItems))
            {
                return false;
            }
        }
        if (unusedItems.Count > 0)
        {
            return false;
        }
        return true;
    }

    private static bool MatchesIngredient(IEnumerable<ItemStack> items, ItemSlot? primaryTool, ItemSlot? offhandTool, CraftingRecipeIngredient ingredient, bool ignoreTools, ISet<ItemStack> unusedItems)
    {
        if (!ingredient.Consume) // TODO: Why does ingredient.IsTool not work but ingredient.Consume does?
        {
            if (ignoreTools)
            {
                return true;;
            }
            if (primaryTool != null && ingredient.SatisfiesAsIngredient(primaryTool.Itemstack, true))
            {
                return true;
            }
            else if (offhandTool != null && ingredient.SatisfiesAsIngredient(offhandTool.Itemstack, true))
            {
                return true;
            }
            return false;
        }
        else
        {
            foreach (ItemStack stack in items)
            {
                if (stack.StackSize > 0 && ingredient.SatisfiesAsIngredient(stack, true))
                {
                    unusedItems.Remove(stack);
                    stack.StackSize -= ingredient.Quantity;
                    return true;
                }
            }
            return false;
        }
    }
}