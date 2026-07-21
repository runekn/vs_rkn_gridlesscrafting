using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using DummySlot = RKN.Crafting.Entities.DummySlot;

namespace RKN.Crafting;

public class RecipeCatalog
{
    private ICoreAPI api;
    private List<GridRecipeWrapper> recipes;

    public RecipeCatalog(ICoreAPI api)
    {
        this.api = api;
        bool gridlesss = api.RcServerConfig().EnableGridless;
        recipes = new(api.World.GridRecipes.Count);
        for (int i = 0; i < api.World.GridRecipes.Count; i++)
        {
            GridRecipe recipe = api.World.GridRecipes[i];
            if (recipe.ResolvedIngredients == null)
            {
                continue;
            }
            // GridRecipe has RecipeId which the game doesn't seem to use itself. I'll steal it to connect FastSearchRecipeByIngredient to index.
            // I tried creating my own FastSearchRecipesByIngredient. But the vanilla map uses ingredients from before variants are resolved. So mine didn't work.
            recipe.RecipeId = i;
            GridRecipeWrapper wrapper = new(recipe, gridlesss, i);
            recipes.Add(wrapper);
        }

        // Fix client-side crate open recipe. Because in the recipes the client receives the fast search ingredient has attribute lidState="opened", which it doesn't on server. 
        foreach (var pair in api.World.FastSearchRecipesByIngredient)
        {
            AssetLocation? code = pair.Key.Code;
            if (code != null && code.Domain.Equals("game") && code.Path.Equals("crate"))
            {
                pair.Key.ResolvedItemStack.Attributes.RemoveAttribute("lidState");
            }
        }
    }

    public GridRecipeWrapper GetRecipeById(int id)
    {
        return recipes[id];
    }

    public List<ScanResult> GetValidRecipes(RecipeInputSlots inputSlots, bool gridless, IPlayer byPlayer)
    {
        List<ScanResult> result = [];
        ItemStack? sample = inputSlots.Items.First(i => i != null && !i.Empty)?.Itemstack;
        if (sample == null)
        {
            return result;
        }
        long start = Environment.TickCount;
        foreach (var pair in api.World.FastSearchRecipesByIngredient)
        {
            if (IngredientSatisfied(pair.Key, sample, null))
            {
                foreach (IRecipeBase recipe in pair.Value)
                {
                    if (recipe is not GridRecipe gridRecipe)
                    {
                        continue;
                    }
                    GridRecipeWrapper wrapper = recipes[gridRecipe.RecipeId];
                    if (MatchesRecipe(inputSlots, wrapper, gridless, byPlayer))
                    {
                        ItemSlot slot = new DummySlot(null);
                        wrapper.RecipeWithoutTools.GenerateOutputStack(inputSlots.Items, slot);
                        result.Add(new ScanResult(wrapper, slot.Itemstack));
                    }
                }
            }
        }
        long time = Environment.TickCount - start;
        api.RcLogger().Debug("Scanning recipes took {0} ms", [time]);
        return result;
    }

    public ScanResult GetScanResult(int i, RecipeInputSlots inputSlots)
    {
        GridRecipeWrapper wrapper = GetRecipeById(i);
        ItemSlot slot = new DummySlot(null);
        wrapper.RecipeWithoutTools.GenerateOutputStack(inputSlots.Items, slot);
        return new ScanResult(wrapper, slot.Itemstack);
    }

    public bool MatchesRecipe(RecipeInputSlots inputSlots, GridRecipeWrapper wrapper, bool gridless, IPlayer byPlayer)
    {
        if (!wrapper.RecipeWithoutTools.Enabled || wrapper.RecipeWithoutTools.ResolvedIngredients == null)
        {
            return false;
        }
        if (gridless)
        {
            // Use custom implementation, because vanilla shapeless matching does not handle scenarios:
                // - Multiple recipe slots of same ingredient fulfilled by one large input stack.
                // - Probably more...
            if (!MatchesRecipeGridless(inputSlots, wrapper.RecipeWithoutTools, byPlayer))
            {
                return false;
            }
        } else
        {
            if (!wrapper.RecipeWithoutTools.Matches(byPlayer, api.World, inputSlots.Items, 3))
            {
                return false;
            }
        }
        foreach (CraftingRecipeIngredient ingredient in wrapper.ToolIngredients)
        {
            if (!IngredientSatisfied(ingredient, inputSlots.PrimaryTool?.Itemstack, wrapper.RecipeWithoutTools) && 
                !IngredientSatisfied(ingredient, inputSlots.OffhandTool?.Itemstack, wrapper.RecipeWithoutTools))
            {
                return false;
            }
        }
        return true;
    }

    private bool MatchesRecipeGridless(RecipeInputSlots inputSlots, GridRecipe recipe, IPlayer byPlayer)
    {
        if (!api.Event.TriggerMatchesRecipe(byPlayer, recipe, inputSlots.Items))
        {
            return false;
        }
        List<ItemStack> clonedItems = inputSlots.Items.Select(i => i?.Itemstack?.Clone()).Where(i => i != null).ToList();
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
            if (!MatchesIngredientGridless(recipe, clonedItems, inputSlots.PrimaryTool, inputSlots.OffhandTool, ingredient, unusedItems))
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

    private bool MatchesIngredientGridless(GridRecipe recipe, IEnumerable<ItemStack> items, ItemSlot? primaryTool, ItemSlot? offhandTool, CraftingRecipeIngredient ingredient, ISet<ItemStack> unusedItems)
    {
        bool satisfied = false; // Instead of just return true on the first item that satisfies ingredient, we need to loop through all so that all satisfying stacks can be removed from unusedItems.
        foreach (ItemStack stack in items)
        {
            if (IngredientSatisfied(ingredient, stack, recipe))
            {
                unusedItems.Remove(stack);
                if (!satisfied)
                {
                    satisfied = true;
                    stack.StackSize -= ingredient.Quantity;
                }
            }
        }
        if (satisfied)
        {
            return true;
        }
        return false;
    }

    private bool IngredientSatisfied(IRecipeIngredientBase ingredient, ItemStack? stack, GridRecipe? recipe)
    {
        return stack != null && stack.StackSize > 0 && ingredient.SatisfiesAsIngredient(stack, true) && (recipe == null || stack.Collectible.MatchesForCrafting(stack, recipe, ingredient as IRecipeIngredient));
    }
}

public class GridRecipeWrapper
{
    public GridRecipe RecipeWithoutTools;
    public GridRecipe RecipeWithTools;
    public int Id;
    public List<CraftingRecipeIngredient> ToolIngredients = [];

    public GridRecipeWrapper(GridRecipe recipe, bool gridless, int id)
    {
        this.RecipeWithTools = recipe;
        this.RecipeWithoutTools = recipe.Clone();
        Id = id;
        if (gridless)
        {
            RecipeWithoutTools.Shapeless = true;
        }
        for (int i = 0; i < RecipeWithoutTools.ResolvedIngredients.Length; i++)
        {
            CraftingRecipeIngredient? ingredient = RecipeWithoutTools.ResolvedIngredients[i];
            if (ingredient != null && !ingredient.Consume)
            {
                RecipeWithoutTools.ResolvedIngredients[i] = null;
                ToolIngredients.Add(ingredient);
            }
        }
    }
}

public record ScanResult(GridRecipeWrapper Wrapper, ItemStack Output);

public record RecipeInputSlots(ItemSlot[] Items, ItemSlot? PrimaryTool, ItemSlot? OffhandTool);