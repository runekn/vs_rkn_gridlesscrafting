using RKN.Crafting.Animation;
using RknCrafting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RKN.Crafting.Entities;

public class BlockEntityCraftingSurface : BlockEntityDisplay
{
    // Initialized fields
    private int slotCount = 9;
    private InventoryGeneric inventory;
    private float craftingSurfaceTimeModifier = 1.0f;
    private RknCraftingConfig config;

    public override InventoryBase Inventory { get { return inventory; }}
    public override string InventoryClassName { get { return "craftingsurface"; }}

    // Runtime fields
    private int selectedRecipe = -1;
    private List<int>? validRecipes;
    private float timeoutTimer;
    private CraftingParams? craftingParams;

    public BlockEntityCraftingSurface()
    {
        inventory = new InventoryDisplayed(this, slotCount, "craftingsurface-0", null);
    }

    public static void OnRecipeConsumed(ICoreClientAPI api, BlockPos pos)
    {
        BlockEntityCraftingSurface entity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(pos);
        entity.MarkMeshesDirty();
        entity.MarkDirty(true);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (validRecipes != null && validRecipes.Count > 0)
        {
            // Don't persist selected recipe after server restart
            // TODO: will this desync on chunk reload?
            selectedRecipe = validRecipes[0];
        }
        craftingSurfaceTimeModifier = api.World.BlockAccessor.GetBlock(Pos.DownCopy(1)).GetBehavior<BlockBehaviorSpawnCraftingSurface>().CraftingTimeModifier;
        config = api.RCConfig();
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        base.OnTesselation(mesher, tessThreadTesselator);
        return true; // Prevent default cube from being rendered
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        //base.GetBlockInfo(forPlayer, sb); // Just food perish stuff
        foreach (ItemSlot itemSlot in inventory)
        {
            if (itemSlot.Empty)
            {
                continue;
            }
            sb.Append(itemSlot.Itemstack.GetName());
            if (itemSlot.Itemstack.StackSize > 1)
            {
                sb.Append(" x");
                sb.Append(itemSlot.Itemstack.StackSize);
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        if (validRecipes != null)
        {
            foreach (int recipeId in validRecipes)
            {
                if (recipeId == selectedRecipe)
                {
                    sb.Append("-) ");
                }
                else
                {
                    sb.Append("   ");
                }
                sb.AppendLine("Recipe: " + Api.RCRecipeCatalog().GetRecipeById(recipeId).Output.ResolvedItemStack.GetName());
            }
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        timeoutTimer = tree.GetFloat("timeoutTimer");
        selectedRecipe = tree.GetInt("selectedRecipe", -1);
        IAttribute validRecipesAttribute = tree["validRecipes"];
        if (validRecipesAttribute != null && validRecipesAttribute is IntArrayAttribute)
        {
            validRecipes = [.. (validRecipesAttribute as IntArrayAttribute).value];
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("timeoutTimer", timeoutTimer);
        tree.SetInt("selectedRecipe", selectedRecipe);
        if (validRecipes != null)
        {
            tree["validRecipes"] = new IntArrayAttribute(validRecipes.ToArray());
        }
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[slotCount][];

        for (int index = 0; index < slotCount; index++)
        {
            float x = 0;
            float z = 0;
            float s = 0.30f;
            ItemSlot itemSlot = inventory[index];
            if (!itemSlot.Empty && itemSlot.Itemstack.StackSize > 0)
            {
                MeshData meshData = getMesh(itemSlot);
                if (meshData != null)
                {
                    float itemSize = GetMeshXZSize(meshData);
                    s = s / itemSize;
                }
            }
            (x, z, s) = index switch
            {
                0 => (0.5f, 0.5f, s),
                1 => (0.2f, 0.2f, s * 0.95f),
                2 => (0.8f, 0.8f, s * 1.02f),
                3 => (0.8f, 0.2f, s * 1.02f),
                4 => (0.2f, 0.8f, s * 1.02f),
                5 => (0.5f, 0.2f, s * 1.02f),
                6 => (0.2f, 0.5f, s * 1.01f),
                7 => (0.9f, 0.5f, s * 0.97f),
                8 => (0.5f, 0.9f, s * 0.98f),
            };

            tfMatrices[index] =
                new Matrixf()
                .Scale(s, s, s)
                .Translate(-0.5f, 0, -0.5f)
                .RotateYDeg(Block.Shape.rotateY)
                .Translate(x / s, 0, z / s)
                .Values
            ;
        }

        return tfMatrices;
    }

    private float GetMeshXZSize(MeshData mesh)
    {
        Vec3f min = new(float.MaxValue, 0, float.MaxValue);
        Vec3f max = new(float.MinValue, 0, float.MinValue);
        for (int i = 0; i < mesh.VerticesCount; i++)
        {
            int index = i * 3;
            float x = mesh.xyz[index];
            float z = mesh.xyz[index + 2];
            min.X = Math.Min(min.X, x);
            min.Z = Math.Min(min.Z, z);
            max.X = Math.Max(max.X, x);
            max.Z = Math.Max(max.Z, z);
        }
        return Math.Max(max.X - min.X, max.Z - min.Z);
    }

    public bool IsCrafting(IPlayer byPlayer)
    {
        return craftingParams?.player == byPlayer;
    }

    public void StartCrafting(IWorldAccessor world, IPlayer byPlayer, BlockCraftingSurface blockCrafting)
    {
        timeoutTimer = 0;
        if (craftingParams != null || selectedRecipe == -1)
        {
            return;
        }
        (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(byPlayer);
        if (items == null || !Api.RCRecipeCatalog().MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
        {
            return;
        }
        bool bulk = byPlayer.Entity.Controls.CtrlKey && Api.RCConfig().EnableBulkCrafting;
        craftingParams = new CraftingParams()
        {
            player = byPlayer,
            animation = Api.RCAnimator().StartCrafting(byPlayer, selectedRecipe, primaryTool, offhandTool),
            bulk = bulk,
            recipeCraftingTimeModifier = GetRecipeOutputCraftingModifier(),
        };
        craftingParams.nextCraftingTime = GetCraftingTime();
        Api.RCLogger().Debug("Crafting {0} by {1}!", [Api.RCRecipeCatalog().GetRecipeById(selectedRecipe).Name, craftingParams.player.PlayerName]);
        if (world.Api.Side == EnumAppSide.Server)
        {
            MarkDirty(); // TODO: why?
        }
    }

    public void OnCraftingStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        timeoutTimer = 0;
        if (Api.Side != EnumAppSide.Server || craftingParams == null)
        {
            return;
        }
        if (craftingParams.bulk && !byPlayer.Entity.Controls.CtrlKey)
        {
            // Player let go of bulk modifier key
            EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
            Api.RCNetwork().StopCrafting(craftingParams.player, enumCraftingAnimation);
            Api.RCAnimator().StopCrafting(craftingParams.player, enumCraftingAnimation);
            ResetState();
            return;
        }
        if (secondsUsed > craftingParams.nextCraftingTime && IsCrafting(byPlayer))
        {
            craftingParams.amount++;
            craftingParams.nextCraftingTime = secondsUsed + GetCraftingTime();

            CreateOutput(world);
            
            // Continue crafting if possible
            (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(craftingParams.player);
            if (items == null || items.Count == 0)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
                EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
                Api.RCNetwork().StopCrafting(craftingParams.player, enumCraftingAnimation);
                Api.RCAnimator().StopCrafting(craftingParams.player, enumCraftingAnimation);
                return;
            }
            else
            {
                validRecipes = Api.RCRecipeCatalog().GetValidRecipesWithoutTools(items);
            }
            Api.RCNetwork().RecipeConsumed(Pos);
            if (items == null || !Api.RCRecipeCatalog().MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
            {
                EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
                Api.RCNetwork().StopCrafting(craftingParams.player, enumCraftingAnimation);
                Api.RCAnimator().StopCrafting(craftingParams.player, enumCraftingAnimation);
                ResetState();
                selectedRecipe = -1;
            }
            MarkDirty(true, null);
        }
    }

    private float GetCraftingTime()
    {
        // TODO: add recipe output modifier
        float @base = craftingParams.bulk ? config.BulkBaseCraftingTimeSeconds : config.BaseCraftingTimeSeconds;
        float consecutiveModifer = craftingParams.amount == 0 ? 1 : Math.Max(config.ConsecutiveCraftingTimeModifierMin, (float) Math.Pow(config.ConsecutiveCraftingTimeModifier, craftingParams.amount));
        float r = @base * craftingSurfaceTimeModifier * craftingParams.recipeCraftingTimeModifier * consecutiveModifer;
        Api.RCLogger().Debug("Next crafting time: {0}", [r]);
        return r;
    }

    private float GetRecipeOutputCraftingModifier()
    {
        JsonObject? recipeProperties = Api.RCRecipeCatalog().GetRecipeById(selectedRecipe).Attributes;
        return recipeProperties != null ? recipeProperties["craftingTimeModifier"].AsFloat(1f) : 1f;
    }

    public void CancelCrafting(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        timeoutTimer = 0;
        if (craftingParams?.player?.ClientId != byPlayer.ClientId)
        {
            return;
        }
        Api.RCLogger().Debug("Cancelled crafting by {0}!", [craftingParams.player.PlayerName]);
        EnumCraftingAnimation anim = GetCraftingAnimation();
        Api.RCAnimator().StopCrafting(byPlayer, anim);
        ResetState();
    }

    private EnumCraftingAnimation GetCraftingAnimation()
    {
        return craftingParams?.animation == null ? EnumCraftingAnimation.HandsGeneric : craftingParams.animation;
    }

    public bool TryPutIngredient(ItemSlot slot, IPlayer byPlayer)
    {
        timeoutTimer = 0;
        if (slot.Itemstack?.Item?.Tool != null)
        {
            return false;
        }
        foreach (ItemSlot invSlot in inventory)
        {
            if (invSlot.CanTakeFrom(slot))
            {
                int quantity = 1;
                if (byPlayer.Entity.Controls.CtrlKey)
                {
                    quantity = slot.StackSize;
                }
                if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    // TODO: Don't pull from slot if gamemode is creative
                }
                if (slot.TryPutInto(Api.World, invSlot, quantity) < 1)
                {
                    return false;
                }
                slot.MarkDirty();

                if (Api.Side == EnumAppSide.Server)
                {
                    (List<ItemSlot>? items, ItemSlot? _, ItemSlot? _) = GetCraftingItems(byPlayer);
                    List<int> recipes = Api.RCRecipeCatalog().GetValidRecipesWithoutTools(items);
                    validRecipes = recipes;
                    selectedRecipe = -1;
                    if (recipes.Count > 0)
                    {
                        selectedRecipe = validRecipes[0];
                    }
                }

                MarkDirty(true, null);
                MarkMeshesDirty();
                return true;
            }
        }
        return false;
    }

    public void SelectNextRecipe()
    {
        if (validRecipes == null || craftingParams != null)
        {
            return;
        }
        for (int i = 0; i < validRecipes.Count; i++)
        {
            if (validRecipes[i] == selectedRecipe)
            {
                selectedRecipe = i == validRecipes.Count-1 ? validRecipes[0] : validRecipes[i+1];
                MarkDirty(true, null);
                return;
            }
        }
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        timeoutTimer += dt;
        if(timeoutTimer >= config.AutoDeleteTimeSeconds)
        {
            Api.World.BlockAccessor.BreakBlock(Pos, null);
        }
    }

    private int ConsumeRecipe(GridRecipe recipe, List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, IWorldAccessor world)
    {
        if (recipe.ResolvedIngredients == null)
        {
            return 0;
        }
        List<ItemSlot> allItems = [.. items];
        if (primaryTool != null) allItems.Add(primaryTool);
        if (offhandTool != null) allItems.Add(offhandTool);
        ItemSlot[] itemsArr = allItems.ToArray();
        int amount = 0;
        while (true)
        {
            foreach (CraftingRecipeIngredient? ingredient in recipe.ResolvedIngredients)
            {
                if (ingredient == null)
                {
                    continue;
                }
                foreach (ItemSlot slot in itemsArr)
                {
                    if (slot.Empty || !ingredient.SatisfiesAsIngredient(slot.Itemstack, true))
                    {
                        continue;
                    }
                    slot.Itemstack.Collectible.OnConsumedByCrafting(itemsArr, slot, recipe, ingredient, craftingParams?.player, ingredient.Quantity);
                }
            }
            amount++;
            if (!craftingParams.bulk || !Api.RCRecipeCatalog().MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
            {
                return amount;
            }
        }
    }

    private (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) GetCraftingItems(IPlayer byPlayer)
    {
        List<ItemSlot> items = inventory.Where(s => s != null && s.StackSize > 0).ToList();
        if (items.Count == 0)
        {
            return (null, null, null);
        }
        IPlayerInventoryManager inventoryManager = byPlayer.InventoryManager;
        ItemSlot? primaryTool = inventoryManager.ActiveTool != null ? inventoryManager.ActiveHotbarSlot : null;
        ItemSlot? offhandTool = inventoryManager.OffhandTool != null ? inventoryManager.OffhandHotbarSlot : null;
        return (items, primaryTool, offhandTool);
    }

    private void CreateOutput(IWorldAccessor world)
    {
        if (craftingParams == null || selectedRecipe == null)
        {
            return;
        }
        (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(craftingParams.player);
        if (items == null || !Api.RCRecipeCatalog().MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
        {
            return;
        }
        GridRecipe gridRecipe = Api.RCRecipeCatalog().GetRecipeById(selectedRecipe);
        ItemStack result = gridRecipe.Output.ResolvedItemStack.Clone();
        if (!result.ResolveBlockOrItem(world))
        {
            return;
        }
        //result.Collectible.OnCreatedByCrafting(Array.Empty<ItemSlot>(), new DummySlot(result), gridRecipe);
        int amount = ConsumeRecipe(gridRecipe, items, primaryTool, offhandTool, world);
        ItemStack output = gridRecipe.Output.ResolvedItemStack.Clone();
        output.StackSize = amount;
        Api.World.SpawnItemEntity(output, Pos);
        Api.RCLogger().Debug("Crafted {0} by {1}!", [gridRecipe.Name, craftingParams.player.PlayerName]);
    }

    private void ResetState()
    {
        craftingParams = null;
        MarkDirty();
    }

    private class CraftingParams
    {
        public IPlayer player;
        public bool bulk;
        public float recipeCraftingTimeModifier;
        public EnumCraftingAnimation animation;
        public float nextCraftingTime;
        public int amount;
    }
}