using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RKN.Crafting;

public class RecipeCatalog
{
    private ICoreAPI api;

    public RecipeCatalog(ICoreAPI api)
    {
        this.api = api;
        // Add ids to recipes. The game doesn't seem to use this field itself, so I steal it so I can use fast grid recipes and still get index in main list.
        for (int i = 0; i < api.World.GridRecipes.Count; i++)
        {
            GridRecipe recipe = api.World.GridRecipes[i];
            recipe.RecipeId = i;
        }
    }

    public GridRecipe GetRecipeById(int id)
    {
        return api.World.GridRecipes[id];
    }

    public List<int> GetValidRecipesWithoutTools(List<ItemSlot> items)
    {
        List<int> result = [];
        ItemStack? sample = items.First()?.Itemstack;
        if (sample == null)
        {
            return result;
        }
        long start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        foreach (var pair in api.World.FastSearchRecipesByIngredient)
        {
            if (IngredientSatisfied(pair.Key, sample, null))
            {
                foreach (var recipe in pair.Value)
                {
                    if (recipe is GridRecipe gridRecipe && MatchesRecipe(items, null, null, gridRecipe, true))
                    {
                        result.Add(recipe.RecipeId);
                    }
                }
            }
        }
        long time = DateTimeOffset.Now.ToUnixTimeMilliseconds() - start;
        api.RCLogger().Debug("Scanning recipes took {0} ms", [time]);
        return result;
    }

    public bool MatchesRecipe(List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, int recipeId, IPlayer byPlayer = null)
    {
        GridRecipe gridRecipe = api.World.GridRecipes[recipeId];
        if (!api.Event.TriggerMatchesRecipe(byPlayer, gridRecipe, items.ToArray()))
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "rkncrafting.missingtools", "fuck you");
            return false;
        }
        return MatchesRecipe(items, primaryTool, offhandTool, gridRecipe, false);
    }

    private bool MatchesRecipe(List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, GridRecipe recipe, bool ignoreTools)
    {
        if (!recipe.Enabled || recipe.ResolvedIngredients == null)
        {
            return false;
        }
        List<ItemStack> clonedItems = items.Select(i => i?.Itemstack?.Clone()).Where(i => i != null).ToList();
        if (clonedItems.Count == 0)
        {
            return false;
        }
        MergeStacks(clonedItems);
        clonedItems = clonedItems.Where(s => s.StackSize > 0).ToList(); // TODO: I don't like creating list again
        ISet<ItemStack> unusedItems = clonedItems.ToHashSet();
        foreach (CraftingRecipeIngredient? ingredient in recipe.ResolvedIngredients)
        {
            if (ingredient == null)
            {
                continue;
            }
            if (!MatchesIngredient(recipe, clonedItems, primaryTool, offhandTool, ingredient, ignoreTools, unusedItems))
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

    protected virtual void MergeStacks(List<ItemStack> stacks)
    {
        for (int i = 1; i < stacks.Count; i++)
        {
            ItemStack stack1 = stacks[i];
            for (int j = 0; j < i; j++)
            {
                ItemStack stack2 = stacks[j];
                if (stack2.Satisfies(stack1))
                {
                    stack2.StackSize += stack1.StackSize;
                    stack1.StackSize = 0;

                }
            }
        }
    }

    private bool MatchesIngredient(GridRecipe recipe, IEnumerable<ItemStack> items, ItemSlot? primaryTool, ItemSlot? offhandTool, CraftingRecipeIngredient ingredient, bool ignoreTools, ISet<ItemStack> unusedItems)
    {
        if (!ingredient.Consume)
        {
            if (ignoreTools)
            {
                return true;
            }
            if (IngredientSatisfied(ingredient, primaryTool?.Itemstack, recipe))
            {
                return true;
            }
            else if (IngredientSatisfied(ingredient, offhandTool?.Itemstack, recipe))
            {
                return true;
            }
            return false;
        }
        else
        {
            foreach (ItemStack stack in items)
            {
                if (IngredientSatisfied(ingredient, stack, recipe))
                {
                    unusedItems.Remove(stack);
                    stack.StackSize -= ingredient.Quantity;
                    return true;
                }
            }
            return false;
        }
    }

    private bool IngredientSatisfied(IRecipeIngredientBase ingredient, ItemStack? stack, GridRecipe? recipe)
    {
        return stack != null && stack.StackSize > 0 && ingredient.SatisfiesAsIngredient(stack, true) && (recipe == null || stack.Collectible.MatchesForCrafting(stack, recipe, ingredient as IRecipeIngredient));
    }
}