using System;
using System.Collections.Generic;
using System.Linq;
using RknCrafting;
using RknCrafting.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using DummySlot = RKN.Crafting.Entities.DummySlot;

namespace RKN.Crafting;

public class RecipeService
{
    public static readonly int RecipeIdNone = -1;
    public static readonly int RecipeIdUnfinished = -2;
    private static readonly AssetLocation UnfinishedCraftAsset = new("rkncrafting", "unfinishedcraft");

    private ICoreAPI api;
    private List<GridRecipeWrapper> recipes;

    public RecipeService(ICoreAPI api)
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

    public List<ICraftingResult> GetValidRecipes(RecipeInputSlots inputSlots)
    {
        List<ICraftingResult> result = [];
        ItemStack? sample = inputSlots.Items.First(i => !i.Empty)?.Itemstack;
        if (sample == null)
        {
            return result;
        }

        if (sample.Item is ItemUnfinishedCraft)
        {
            return GetValidForUnfinishedCraft(sample, inputSlots);
        }
        
        long start = Environment.TickCount;
        bool gridless = api.RcServerConfig().EnableGridless;
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
                    List<int> usedTools = [];
                    if (MatchesRecipe(inputSlots, wrapper, gridless, usedTools))
                    {
                        result.Add(CreateResult(wrapper, null, inputSlots, usedTools, true));
                    }
                }
            }
        }
        long time = Environment.TickCount - start;
        api.RcLogger().Debug("Scanning recipes took {0} ms", [time]);
        return result;
    }

    private List<ICraftingResult> GetValidForUnfinishedCraft(ItemStack sample, RecipeInputSlots inputSlots)
    {
        if (inputSlots.Items.Count(i => !i.Empty) > 1)
        {
            return [];
        }

        sample = sample.Clone();
        ItemStack output = sample.Attributes.GetItemstack("output");
        List<int> usedTools = sample.Attributes.GetString("usedTools").Split(":").Select(int.Parse).ToList();
        int recipeId = sample.Attributes.GetAsInt("recipe");
        GridRecipeWrapper wrapper = GetRecipeById(recipeId);
        bool matchedAny = false;
        for (int i = 0; i < wrapper.ToolIngredients.Count; i++)
        {
            if (usedTools.Contains(i))
            {
                continue;
            }
            CraftingRecipeIngredient ingredient = wrapper.ToolIngredients[i];
            if (!IngredientSatisfied(ingredient, inputSlots.PrimaryTool?.Itemstack, null) &&
                !IngredientSatisfied(ingredient, inputSlots.OffhandTool?.Itemstack, null))
            {
                continue;
            }
            matchedAny = true;
            usedTools.Add(i);
        }

        if (!matchedAny)
        {
            return [];
        }
        return [CreateResult(wrapper, output, inputSlots, usedTools, false)];
    }

    public ICraftingResult? GetRecipe(int i, RecipeInputSlots inputSlots)
    {
        if (i == RecipeIdUnfinished)
        {
            ItemStack sample = inputSlots.Items.First(s =>  !s.Empty)?.Itemstack;
            List<ICraftingResult> r = GetValidForUnfinishedCraft(sample, inputSlots);
            return r.Count > 0 ? r[0] : null;
        }
        GridRecipeWrapper wrapper = GetRecipeById(i);
        List<int> usedToolIngredients = [];
        if (!MatchesRecipe(inputSlots, wrapper, api.RcServerConfig().EnableGridless, usedToolIngredients))
        {
            return null;
        }

        return CreateResult(wrapper, null, inputSlots, usedToolIngredients, true);
    }

    private bool MatchesRecipe(RecipeInputSlots inputSlots, GridRecipeWrapper wrapper, bool gridless, List<int>? usedTools)
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
            if (!MatchesRecipeGridless(inputSlots, wrapper.RecipeWithoutTools))
            {
                return false;
            }
        } else
        {
            if (!wrapper.RecipeWithoutTools.Matches(inputSlots.player, api.World, inputSlots.Items, 3))
            {
                return false;
            }
        }

        return ToolsMatchesRecipe(wrapper.ToolIngredients, usedTools, inputSlots, true);
    }

    private bool ToolsMatchesRecipe(List<CraftingRecipeIngredient> toolIngredients, List<int>? usedTools, RecipeInputSlots inputSlots, bool updateUsedTools)
    {
        if (toolIngredients.Count == 0)
        {
            return true;
        }
        bool matchedAny = false;
        for (int index = 0; index < toolIngredients.Count; index++)
        {
            if (usedTools?.Contains(index) ?? false)
            {
                continue;
            }
            CraftingRecipeIngredient ingredient = toolIngredients[index];
            if (!IngredientSatisfied(ingredient, inputSlots.PrimaryTool?.Itemstack, null) &&
                !IngredientSatisfied(ingredient, inputSlots.OffhandTool?.Itemstack, null))
            {
                continue;
            }
            matchedAny = true;
            if (updateUsedTools && usedTools != null)
            {
                usedTools.Add(index);
            }
        }

        return matchedAny;
    }

    private bool MatchesRecipeGridless(RecipeInputSlots inputSlots, GridRecipe recipe)
    {
        if (!api.Event.TriggerMatchesRecipe(inputSlots.player, recipe, inputSlots.Items))
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

    private void MergeStacks(List<ItemStack> stacks)
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

    private ICraftingResult CreateResult(GridRecipeWrapper wrapper, ItemStack? output, RecipeInputSlots inputSlots, List<int>? outputUsedTools, bool first)
    {
        if (output == null)
        {
            ItemSlot slot = new DummySlot(null);
            wrapper.RecipeWithoutTools.GenerateOutputStack(inputSlots.Items, slot);
            output = slot.Itemstack;
        }
        ItemStack actualOutput = output;
        
        if (outputUsedTools != null || outputUsedTools.Count < wrapper.ToolIngredients.Count)
        {
            actualOutput = new(api.World.GetItem(UnfinishedCraftAsset));
            actualOutput.Attributes.SetItemstack("output", output);
            actualOutput.Attributes.SetInt("recipe", wrapper.Id);
            actualOutput.Attributes.SetString("usedTools", string.Join(":", outputUsedTools));
        }

        if (first)
        {
            return new GridRecipeCraftingResult(wrapper, output, actualOutput, this);    
        }
        List<int> usedTools = inputSlots.Items.First(s => !s.Empty)
            .Itemstack
            .Attributes
            .GetString("usedTools")
            .Split(":")
            .Select(int.Parse)
            .ToList();
        return new UnfinishedCraftingResult(wrapper, usedTools, actualOutput, output, this);     
    }
    
    private int ConsumeRecipe(GridRecipeWrapper wrapper, RecipeInputSlots inputSlots, ItemStack result, bool bulk)
    {
        if (api.RcServerConfig().EnableGridless)
        {
            return ConsumeRecipeGridless(wrapper.RecipeWithoutTools.ResolvedIngredients, wrapper, inputSlots, result, bulk);
        }
        int amount = 0;
        while (
            MatchesRecipe(inputSlots, wrapper, false, null) && 
            wrapper.RecipeWithoutTools.ConsumeInput(inputSlots.player, inputSlots.Items, 3) &&
            ConsumeRecipeTools(wrapper, inputSlots))
        {
            amount++;
            if (!bulk)
            {
                break;
            }
        }

        return amount;
    }

    private bool ConsumeRecipeTools(GridRecipeWrapper wrapper, RecipeInputSlots inputSlots)
    {
        if (wrapper.ToolIngredients.Count == 0)
        {
            return true;
        }
        bool anyMatch = false;
        foreach (CraftingRecipeIngredient ingredient in wrapper.ToolIngredients)
        {
            if (inputSlots.PrimaryTool != null && ingredient.SatisfiesAsIngredient(inputSlots.PrimaryTool.Itemstack))
            {
                inputSlots.PrimaryTool.Itemstack.Collectible.OnConsumedByCrafting(inputSlots.Items, inputSlots.PrimaryTool, wrapper.RecipeWithTools, ingredient, inputSlots.player, ingredient.Quantity);
                anyMatch = true;
            }
            else if (inputSlots.OffhandTool != null && ingredient.SatisfiesAsIngredient(inputSlots.OffhandTool.Itemstack))
            {
                inputSlots.OffhandTool.Itemstack.Collectible.OnConsumedByCrafting(inputSlots.Items, inputSlots.OffhandTool, wrapper.RecipeWithTools, ingredient, inputSlots.player, ingredient.Quantity);
                anyMatch = true;
            }
        }
        return anyMatch;
    }

    private int ConsumeRecipeGridless(CraftingRecipeIngredient?[] ingredients, GridRecipeWrapper wrapper, RecipeInputSlots inputSlots, ItemStack result, bool bulk)
    {
        GridRecipe recipe = wrapper.RecipeWithoutTools;
        List<ItemSlot> allItems = [.. inputSlots.Items];
        if (inputSlots.PrimaryTool != null) allItems.Add(inputSlots.PrimaryTool);
        if (inputSlots.OffhandTool != null) allItems.Add(inputSlots.OffhandTool);
        ItemSlot[] itemsArr = allItems.ToArray();
        if (result.Collectible.ConsumeCraftingIngredients(itemsArr, new DummySlot(result), recipe))
        {
            api.RcLogger().Debug("Recipe {0} was rejected by collectible!", recipe.Name);
            return 0;
        }
        int amount = 0;
        while (true)
        {
            foreach (CraftingRecipeIngredient? ingredient in ingredients)
            {
                if (ingredient == null)
                {
                    continue;
                }
                int quantity = ingredient.Quantity;
                foreach (ItemSlot slot in itemsArr)
                {
                    if (slot.Empty || !ingredient.SatisfiesAsIngredient(slot.Itemstack, false))
                    {
                        continue;
                    }
                    int size = slot.Itemstack.StackSize;
                    slot.Itemstack.Collectible.OnConsumedByCrafting(itemsArr, slot, recipe, ingredient, inputSlots.player, quantity);
                    quantity -= size;
                    if (quantity <= 0)
                    {
                        break;
                    }
                }
            }
            amount++;
            if (!bulk || 
                !inputSlots.Items.Any(i => i != null) || 
                !MatchesRecipe(inputSlots, wrapper, true, null))
            {
                return amount;
            }
        }
    }

    private class UnfinishedCraftingResult : ICraftingResult
    {
        private readonly RecipeService service;
        private readonly ItemStack output;
        private readonly ItemStack finalOutput;
        private readonly List<int> usedToolsIndexes;
        private readonly GridRecipeWrapper wrapper;

        public UnfinishedCraftingResult(GridRecipeWrapper wrapper, List<int> usedToolsIndexes, ItemStack output, ItemStack finalOutput, RecipeService service)
        {
            this.wrapper = wrapper;
            this.usedToolsIndexes = usedToolsIndexes;
            this.output = output;
            this.finalOutput = finalOutput;
            finalOutput.ResolveBlockOrItem(service.api.World); // Attribute deserializer doesn't do this apparently
            this.service = service;
        }

        public string Name => finalOutput.GetName();
        public int Id => RecipeIdUnfinished;
        public ItemStack SelectionItemStack => finalOutput;
        public float CraftingTimeModifier
        {
            get
            {
                JsonObject attributes = finalOutput.Collectible.Attributes;
                return attributes["craftingTimeModifier"].AsFloat(1f);
            }
        }

        public bool Matches(RecipeInputSlots inputSlots)
        {
            if (inputSlots.Items.Count(s => !s.Empty) != 1)
            {
                return false;
            }
            return service.ToolsMatchesRecipe(wrapper.ToolIngredients, usedToolsIndexes, inputSlots, false);
        }

        public ItemStack? GenerateOutput(RecipeInputSlots inputSlots, bool bulk)
        {
            ItemSlot slot = inputSlots.Items.First(s => !s.Empty);
            int amount = 0;
            while (
                service.ToolsMatchesRecipe(wrapper.ToolIngredients, usedToolsIndexes, inputSlots, false) && 
                service.ConsumeRecipeTools(wrapper, inputSlots))
            {
                slot.TakeOut(1);
                amount++;
                if (!bulk)
                {
                    break;
                }
            }
            ItemStack outputStack = finalOutput.Clone();
            outputStack.StackSize = amount;
            return outputStack;
        }
    }

    private class GridRecipeCraftingResult : ICraftingResult
    {
        private readonly RecipeService service;
        private readonly GridRecipeWrapper wrapper;
        private readonly ItemStack output;
        private readonly ItemStack intermediateOutput;
        private readonly bool gridless;

        public GridRecipeCraftingResult(GridRecipeWrapper wrapper, ItemStack output, ItemStack intermediateOutput, RecipeService service)
        {
            this.wrapper = wrapper;
            this.output = output;
            this.intermediateOutput = intermediateOutput;
            this.gridless = service.api.RcServerConfig().EnableGridless;
            this.service = service;
        }

        public string Name => wrapper.RecipeWithTools.Name;
        public int Id => wrapper.Id;
        public ItemStack SelectionItemStack => output;

        public float CraftingTimeModifier
        {
            get
            {
                JsonObject attributes = output.Collectible.Attributes;
                return attributes["craftingTimeModifier"].AsFloat(1f);
            }
        }

        public bool Matches(RecipeInputSlots inputSlots)
        {
            return service.MatchesRecipe(inputSlots, wrapper, gridless, null);
        }

        public ItemStack? GenerateOutput(RecipeInputSlots inputSlots, bool bulk)
        {
            int amount = service.ConsumeRecipe(wrapper, inputSlots, output, bulk);
            if (amount <= 0)
            {
                return null;
            }
            ItemStack outputStack = intermediateOutput.Clone();
            outputStack.StackSize *= amount;
            return outputStack;
        }
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
        RecipeWithTools = recipe;
        RecipeWithoutTools = recipe.Clone();
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

public interface ICraftingResult
{
    string Name { get; }
    int Id { get; }
    ItemStack SelectionItemStack { get; }
    float CraftingTimeModifier { get; }
    bool Matches(RecipeInputSlots inputSlots);
    ItemStack? GenerateOutput(RecipeInputSlots inputSlots, bool bulk);
}

public record RecipeInputSlots(ItemSlot[] Items, IPlayer player, ItemSlot? PrimaryTool, ItemSlot? OffhandTool);