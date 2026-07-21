using RKN.Crafting.Animation;
using RknCrafting;
using RknCrafting.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "craftingsurface";
    public override string AttributeTransformCode => "craftingIngredientTransform";

    // Client Runtime fields
    private int selectedRecipe = -1;
    private List<ScanResult>? validRecipes;
    private BlockFacing? lastFacing;
    private EnumTool? lastTool;
    private bool dirtyRecipes = true;
    private RecipeSelectionDialog? recipeSelectionDialog;

    private CraftingParams? craftingParams;

    // Server-client Runtime fields
    private float timeoutTimer;

    public BlockEntityCraftingSurface()
    {
        inventory = new InventoryDisplayed(this, slotCount, "craftingsurface-0", null);
    }

    public static void OnInventoryUpdated(ICoreClientAPI api, BlockPos pos)
    {
        BlockEntityCraftingSurface entity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(pos);
        if (entity == null)
        {
            api.RcLogger().Debug("Got OnInventoryUpdated for non-existing block: [{0},{1},{2}]", pos.X, pos.Y, pos.Z);
            return;
        }

        if (entity.recipeSelectionDialog != null && entity.recipeSelectionDialog.IsOpened())
        {
            entity.recipeSelectionDialog.TryClose();
        }
        entity.dirtyRecipes = true;
        entity.MarkMeshesDirty();
        entity.MarkDirty(true);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        craftingSurfaceTimeModifier = api.World.BlockAccessor.GetBlock(Pos.DownCopy(1)).GetBehavior<BlockBehaviorSpawnCraftingSurface>().CraftingTimeModifier;
        config = api.RcConfig();
        craftingParams = null; // Override FromTreeAttributes because we don't want dummy craftingParams on server
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        base.OnTesselation(mesher, tessThreadTesselator);
        return true; // Prevent default cube from being rendered
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        // FYI: GetBlockInfo Is called every 500 milliseconds it seems.
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
        var scanResult = GetSelectedRecipe();
        if (scanResult == null)
        {
            return;
        }
        sb.Append("Selected: ").AppendLine(scanResult.Output?.GetName());
        if (validRecipes.Count > 1)
        {
            sb.Append(validRecipes.Count - 1).Append(" more valid recipes");
        }
    }

    protected override MeshData getOrCreateMesh(ItemSlot slot, int index)
    {
        // Fix crates. Because they do not have proper config to display their custom mesh
        if (slot.Itemstack?.Block is BlockCrate crate)
        {
            MeshData mesh = getMesh(slot);
            if (mesh != null)
                return mesh;
            ItemStack stack = slot.Itemstack;
            string type = stack.Attributes.GetString("type");
            mesh = crate.GenMesh(capi, stack, type, null, stack.Attributes.GetString("lidState"), crate.Props[type].Shape);
            applyDefaultTranforms(stack, mesh);
            MeshCache[getMeshCacheKey(slot)] = mesh;
            return mesh;
        }
        return base.getOrCreateMesh(slot, index);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        timeoutTimer = tree.GetFloat("timeoutTimer");
        if (tree.GetBool("isCrafting"))
        {
            craftingParams ??= new CraftingParams()
            {
                OtherPlayer = true
            };
        }
        else if (craftingParams?.OtherPlayer == true)
        {
            craftingParams = null;
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("timeoutTimer", timeoutTimer);
        tree.SetBool("isCrafting", craftingParams != null);
    }

    protected override float[][] genTransformationMatrices()
    {
        return CraftingItemRenderer.GenTransformationMatrices(inventory, AttributeTransformCode, config.EnableGridless, Block, getMesh);
    }

    public bool IsCrafting(IPlayer byPlayer)
    {
        return craftingParams?.Player.ClientId == byPlayer.ClientId;
    }

    public void ClientStartedCrafting(IPlayer byPlayer, EnumCraftingAnimation animation, float recipeModifier, int recipe, bool bulk, float nextCraftingTime, BlockFacing? blockFacing)
    {
        craftingParams = new CraftingParams()
        {
            Player = byPlayer,
            Recipe = Api.RcRecipeCatalog().GetScanResult(recipe, GetCraftingInputSlots(byPlayer, blockFacing)),
            Animation = animation,
            Bulk = bulk,
            RecipeCraftingTimeModifier = recipeModifier,
            NextCraftingTime = nextCraftingTime,
            Facing = blockFacing
        };
    }

    public bool StartCrafting(IWorldAccessor world, IPlayer byPlayer)
    {
        timeoutTimer = 0;
        if (Api.Side == EnumAppSide.Server)
        {
            return true;
        }

        if (craftingParams != null)
        {
            if (!IsCrafting(byPlayer))
            {
                ClientError("surfacealreadycrafting");
            }
            return false;
        }
        ScanResult? result = GetSelectedRecipe();
        if (result == null)
        {
            return false;
        }
        RecipeInputSlots inputSlots = GetCraftingInputSlots(byPlayer, lastFacing);
        if (inputSlots == null || !Api.RcRecipeCatalog().MatchesRecipe(inputSlots, result.Wrapper, config.EnableGridless, byPlayer))
        {
            ClientError("missingreciperequirement");
            return false;
        }
        bool bulk = byPlayer.Entity.Controls.CtrlKey && Api.RcConfig().EnableBulkCrafting;
        craftingParams = new CraftingParams()
        {
            Player = byPlayer,
            Facing = lastFacing,
            Recipe = result,
            Animation = Api.RcAnimator().StartCrafting(byPlayer, selectedRecipe, inputSlots.PrimaryTool, inputSlots.OffhandTool),
            Bulk = bulk,
            RecipeCraftingTimeModifier = GetRecipeOutputCraftingModifier(),
        };
        craftingParams.NextCraftingTime = GetCraftingTime();
        Api.RcNetwork().ClientStartedCrafting(craftingParams, Pos);
        Api.RcLogger().Debug("Crafting {0} by {1}!", craftingParams.Recipe.Wrapper.RecipeWithoutTools.Name, craftingParams.Player.PlayerName);
        return true;
    }

    public bool OnCraftingStep(float secondsUsed, IPlayer byPlayer)
    {
        timeoutTimer = 0;
        if (Api.Side != EnumAppSide.Server || craftingParams == null)
        {
            return true;
        }
        if (craftingParams.Bulk && !byPlayer.Entity.Controls.CtrlKey)
        {
            // Player let go of bulk modifier key
            EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
            Api.RcNetwork().StopCrafting(craftingParams.Player, enumCraftingAnimation, Pos);
            Api.RcAnimator().StopCrafting(craftingParams.Player, enumCraftingAnimation);
            ResetState();
            return false;
        }
        if (secondsUsed > craftingParams.NextCraftingTime && IsCrafting(byPlayer))
        {
            craftingParams.Amount++;
            craftingParams.NextCraftingTime = secondsUsed + GetCraftingTime();

            CreateOutput();

            // Continue crafting if possible
            RecipeInputSlots inputSlots = GetCraftingInputSlots(craftingParams.Player, craftingParams.Facing);
            if (inputSlots == null)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
                EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
                Api.RcNetwork().StopCrafting(craftingParams.Player, enumCraftingAnimation, Pos);
                Api.RcAnimator().StopCrafting(craftingParams.Player, enumCraftingAnimation);
                return false;
            }
            if (!Api.RcRecipeCatalog().MatchesRecipe(inputSlots, craftingParams.Recipe.Wrapper, config.EnableGridless, byPlayer))
            {
                EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
                Api.RcNetwork().StopCrafting(craftingParams.Player, enumCraftingAnimation, Pos);
                Api.RcAnimator().StopCrafting(craftingParams.Player, enumCraftingAnimation);
                ResetState();
                return false;
            }
            MarkDirty(true);
        }

        return true;
    }

    public void CancelCrafting(IPlayer byPlayer)
    {
        timeoutTimer = 0;
        if (craftingParams?.Player?.ClientId != byPlayer.ClientId)
        {
            return;
        }
        Api.RcLogger().Debug("Cancelled crafting by {0}!", craftingParams.Player.PlayerName);
        EnumCraftingAnimation anim = GetCraftingAnimation();
        Api.RcAnimator().StopCrafting(byPlayer, anim);
        ResetState();
    }

    public void ClientStopCrafting(EnumCraftingAnimation anim)
    {
        timeoutTimer = 0;
        IPlayer player = capi.World.Player;
        capi.RcPauseInteractions();
        Api.RcLogger().Debug("Stop crafting by {0}!", player.PlayerName);
        Api.RcAnimator().StopCrafting(player, anim);
        ResetState();
    }

    private EnumCraftingAnimation GetCraftingAnimation()
    {
        return craftingParams?.Animation == null ? EnumCraftingAnimation.HandsGeneric : craftingParams.Animation;
    }

    public bool TryPutIngredient(ItemSlot slot, IPlayer? byPlayer = null, int selectionBoxIndex = 0)
    {
        timeoutTimer = 0;
        if (slot.Itemstack?.Item?.Tool != null || craftingParams != null)
        {
            return false;
        }
        Api.RcLogger().Debug("Inserting into slot {0}", selectionBoxIndex);
        ItemSlot? invSlot = GetInventorySlot(invSlot => invSlot.CanTakeFrom(slot), selectionBoxIndex);
        if (invSlot == null)
        {
            ClientError("surfacefull");
            return false;
        }
        int quantity = 1;
        if (byPlayer != null && byPlayer.Entity.Controls.CtrlKey)
        {
            quantity = slot.StackSize;
        }
        if (byPlayer != null && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
        {
            // TODO: Don't pull from slot if gamemode is creative
        }
        if (slot.TryPutInto(Api.World, invSlot, quantity) < 1)
        {
            ClientError("surfacefull");
            return false;
        }
        if (byPlayer != null)
        {
            Api.World.PlaySoundAt(invSlot.Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
        }
        slot.MarkDirty();
        //dirtyRecipes = true; // we also do this through OnInventoryUpdated. So don't do it here or we will scan recipes twice
        MarkDirty(true, byPlayer);
        MarkMeshesDirty();
        return true;
    }

    public bool TryTakeIngredient(ItemSlot slot, IPlayer? byPlayer = null, int selectionBoxIndex = 0)
    {
        timeoutTimer = 0;
        if (craftingParams != null)
        {
            return false;
        }
        Api.RcLogger().Debug("Taking into slot {0}", selectionBoxIndex);
        ItemSlot? invSlot = GetInventorySlot(invSlot => slot.CanTakeFrom(invSlot), selectionBoxIndex, true);
        if (invSlot == null)
        {
            ClientError("surfaceempty");
            return false;
        }
        int quantity = 1;
        if (byPlayer != null && byPlayer.Entity.Controls.CtrlKey)
        {
            quantity = invSlot.StackSize;
        }
        if (invSlot.TryPutInto(Api.World, slot, quantity) < 1)
        {
            ClientError("surfaceempty");
            return false;
        }
        if (byPlayer != null)
        {
            Api.World.PlaySoundAt(invSlot.Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
        }
        slot.MarkDirty();
        if (inventory.Empty)
        {
            Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
            return true;
        }
        //dirtyRecipes = true; // we also do this through OnInventoryUpdated. So don't do it here or we will scan recipes twice
        MarkDirty(true, byPlayer);
        MarkMeshesDirty();
        return true;
    }

    private ItemSlot? GetInventorySlot(Predicate<ItemSlot> test, int selectionBoxIndex, bool reverse = false)
    {
        if (config.EnableGridless)
        {
            IEnumerable<ItemSlot> enumerable = reverse ? inventory.Reverse() : inventory;
            foreach (ItemSlot invSlot in enumerable)
            {
                if (test(invSlot))
                {
                    return invSlot;
                }
            }
        }
        else
        {
            ItemSlot invSlot = inventory[selectionBoxIndex];
            if (test(invSlot))
            {
                return invSlot;
            }
        }
        return null;
    }

    public void OpenRecipeSelection()
    {
        if (recipeSelectionDialog == null)
        {
            recipeSelectionDialog = new RecipeSelectionDialog(capi, Pos); 
        }
        recipeSelectionDialog
            .TryOpen(validRecipes.ToArray(), i =>
            {
                selectedRecipe = i;
                Api.RcLogger().Debug("selected: {0} {1}", i, GetSelectedRecipe().Wrapper.RecipeWithTools.Name);
                recipeSelectionDialog.TryClose();
            });
    }

    public void CheckIfUpdateRecipes(IPlayer byPlayer)
    {
        if (Api.Side == EnumAppSide.Server)
        {
            return;
        }
        if (!config.EnableGridless) {
            BlockFacing blockFacing = GetBlockOrientation(Pos, byPlayer);
            if (blockFacing != lastFacing)
            {
                lastFacing = blockFacing;
                dirtyRecipes = true;
            }
        }
        if (byPlayer.InventoryManager.ActiveTool != lastTool)
        {
            lastTool = byPlayer.InventoryManager.ActiveTool;
            dirtyRecipes = true;
        }

        if (dirtyRecipes && craftingParams == null)
        {
            UpdateValidRecipes(byPlayer);
            dirtyRecipes = false;
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

    private float GetCraftingTime()
    {
        float @base = craftingParams.Bulk ? config.BulkBaseCraftingTimeSeconds : config.BaseCraftingTimeSeconds;
        float consecutiveModifer = craftingParams.Amount == 0 ? 1 : Math.Max(config.ConsecutiveCraftingTimeModifierMin, (float)Math.Pow(config.ConsecutiveCraftingTimeModifier, craftingParams.Amount));
        float r = @base * craftingSurfaceTimeModifier * craftingParams.RecipeCraftingTimeModifier * consecutiveModifer;
        Api.RcLogger().Debug("Next crafting time: {0}", r);
        return r;
    }

    private float GetRecipeOutputCraftingModifier()
    {
        JsonObject? recipeProperties = Api.RcRecipeCatalog().GetRecipeById(selectedRecipe).RecipeWithoutTools.Attributes;
        return recipeProperties != null ? recipeProperties["craftingTimeModifier"].AsFloat(1f) : 1f;
    }

    private void UpdateValidRecipes(IPlayer byPlayer)
    {
        if (Api.Side == EnumAppSide.Server)
        {
            return;
        }
        RecipeInputSlots inputSlots = GetCraftingInputSlots(byPlayer, lastFacing);
        if (inputSlots == null) {
            validRecipes = [];
            selectedRecipe = -1;
            return;
        }
        List<ScanResult> recipes = Api.RcRecipeCatalog().GetValidRecipes(inputSlots, config.EnableGridless, capi.World.Player);
        validRecipes = recipes;
        if (recipes.Count == 0)
        {
            selectedRecipe = -1;
        }
        else if (GetSelectedRecipe() == null)
        {
            selectedRecipe = validRecipes.First().Wrapper.Id;
        }
    }

    private void CreateOutput()
    {
        if (craftingParams == null)
        {
            return;
        }
        RecipeInputSlots inputSlots = GetCraftingInputSlots(craftingParams.Player, craftingParams.Facing);
        if (inputSlots?.Items == null || !Api.RcRecipeCatalog().MatchesRecipe(inputSlots, craftingParams.Recipe.Wrapper, config.EnableGridless, craftingParams.Player))
        {
            return;
        }

        ScanResult result = craftingParams.Recipe;
        ItemStack output = result.Output.Clone();
        int amount = ConsumeRecipe(result.Wrapper, inputSlots, output);
        if (amount == 0)
        {
            return;
        }
        output.StackSize *= amount;
        Api.World.SpawnItemEntity(output, Pos);
        Api.RcLogger().Debug("Crafted {0} by {1}!", result.Wrapper.RecipeWithoutTools.Name, craftingParams.Player.PlayerName);
    }

    private int ConsumeRecipe(GridRecipeWrapper wrapper, RecipeInputSlots inputSlots, ItemStack result)
    {
        if (config.EnableGridless)
        {
            return ConsumeRecipeGridless(wrapper.RecipeWithTools, inputSlots, result);
        }
        int amount = 0;
        RecipeCatalog recipeCatalog = Api.RcRecipeCatalog();
        while (
            recipeCatalog.MatchesRecipe(inputSlots, wrapper, config.EnableGridless, craftingParams.Player) && 
            wrapper.RecipeWithoutTools.ConsumeInput(craftingParams.Player, inputSlots.Items, 3) &&
            ConsumeRecipeTools(wrapper, inputSlots))
        {
            
            amount++;
            if (!craftingParams.Bulk)
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
        foreach (CraftingRecipeIngredient ingredient in wrapper.ToolIngredients)
        {
            if (inputSlots.PrimaryTool != null && ingredient.SatisfiesAsIngredient(inputSlots.PrimaryTool.Itemstack))
            {
                inputSlots.PrimaryTool.Itemstack.Collectible.OnConsumedByCrafting(inputSlots.Items, inputSlots.PrimaryTool, wrapper.RecipeWithTools, ingredient, craftingParams.Player, ingredient.Quantity);
            }
            else if (inputSlots.OffhandTool != null && ingredient.SatisfiesAsIngredient(inputSlots.OffhandTool.Itemstack))
            {
                inputSlots.OffhandTool.Itemstack.Collectible.OnConsumedByCrafting(inputSlots.Items, inputSlots.OffhandTool, wrapper.RecipeWithTools, ingredient, craftingParams.Player, ingredient.Quantity);
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private int ConsumeRecipeGridless(GridRecipe recipe, RecipeInputSlots inputSlots, ItemStack result)
    {
        if (recipe.ResolvedIngredients == null || craftingParams == null)
        {
            return 0;
        }
        List<ItemSlot> allItems = [.. inputSlots.Items];
        if (inputSlots.PrimaryTool != null) allItems.Add(inputSlots.PrimaryTool);
        if (inputSlots.OffhandTool != null) allItems.Add(inputSlots.OffhandTool);
        ItemSlot[] itemsArr = allItems.ToArray();
        if (result.Collectible.ConsumeCraftingIngredients(itemsArr, new DummySlot(result), recipe))
        {
            Api.RcLogger().Debug("Recipe {0} was rejected by collectible!", recipe.Name);
            return 0;
        }
        int amount = 0;
        while (true)
        {
            foreach (CraftingRecipeIngredient? ingredient in recipe.ResolvedIngredients)
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
                    slot.Itemstack.Collectible.OnConsumedByCrafting(itemsArr, slot, recipe, ingredient, craftingParams.Player, quantity);
                    quantity -= size;
                    if (quantity <= 0)
                    {
                        break;
                    }
                }
            }
            amount++;
            if (!craftingParams.Bulk || !inputSlots.Items.Any(i => i != null) || !Api.RcRecipeCatalog().MatchesRecipe(inputSlots, craftingParams.Recipe.Wrapper, config.EnableGridless, craftingParams.Player))
            {
                return amount;
            }
        }
    }

    private RecipeInputSlots? GetCraftingInputSlots(IPlayer? byPlayer, BlockFacing? facing)
    {
        if (inventory.All(s => s == null || s.StackSize == 0))
        {
            return null;
        }
        ItemSlot? primaryTool = null;
        ItemSlot? offhandTool = null;
        if (byPlayer != null)
        {
            IPlayerInventoryManager inventoryManager = byPlayer.InventoryManager;
            primaryTool = inventoryManager.ActiveTool != null ? inventoryManager.ActiveHotbarSlot : null;
            offhandTool = inventoryManager.OffhandTool != null ? inventoryManager.OffhandHotbarSlot : null;
        }
        return new(RearrangeGridByFacing(inventory.ToArray(), facing), primaryTool, offhandTool);
    }

    /**
        SOUTH (default)     Resulting indexes
        0 1 2		        012345678
        3 4 5
        6 7 8

        NORTH
        8 7 6		        876543210
        5 4 3
        2 1 0

        WEST
        6 3 0		        630741852
        7 4 1
        8 5 2

        EAST
        2 5 8		        258147036
        1 4 7
        0 3 6
     */

    private ItemSlot[] RearrangeGridByFacing(ItemSlot[] a, BlockFacing? facing)
    {
        if (facing == null || config.EnableGridless || facing == BlockFacing.SOUTH)
        {
            return a;
        }
        if (facing == BlockFacing.NORTH)
        {
            return [a[8], a[7], a[6], a[5], a[4], a[3], a[2], a[1], a[0]];
        }
        if (facing == BlockFacing.EAST)
        {
            return [a[6], a[3], a[0], a[7], a[4], a[1], a[8], a[5], a[2]];
        }
        else // WEST
        {
            return [a[2], a[5], a[8], a[1], a[4], a[7], a[0], a[3], a[6]];
        }
    }

    private static BlockFacing GetBlockOrientation(BlockPos blockPos, IPlayer? byPlayer)
    {
        if (byPlayer == null)
        {
            return BlockFacing.NORTH;
        }

        EntityPos playerPos = byPlayer.Entity.Pos;
        Vec3f facingVector = new Vec3f((float)playerPos.X - (blockPos.X + 0.5f), 0, (float)playerPos.Z - (blockPos.Z + 0.5f)).Normalize();
        if (facingVector.Z > Math.Abs(facingVector.X))
        {
            return BlockFacing.SOUTH;
        }
        else if (-facingVector.Z > Math.Abs(facingVector.X))
        {
            return BlockFacing.NORTH;
        }
        else if (facingVector.X > Math.Abs(facingVector.Z))
        {
            return BlockFacing.EAST;
        }
        else
        {
            return BlockFacing.WEST;
        }
    }

    private void ResetState()
    {
        craftingParams = null;
        dirtyRecipes = true;
        MarkDirty();
    }

    private ScanResult? GetSelectedRecipe()
    {
        if (selectedRecipe == -1 || validRecipes?.Count == 0)
            return null;

        return validRecipes.Where(r => r.Wrapper.Id == selectedRecipe).First();
    }

    private void ClientError(string error)
    {
        capi?.TriggerIngameError(this, "rkncrafting." + error, Lang.Get("rkncrafting:error-" + error));
    }
}

public class CraftingParams
{
    public IPlayer Player;
    public bool Bulk;
    public bool OtherPlayer;
    public float RecipeCraftingTimeModifier;
    public EnumCraftingAnimation Animation;
    public ScanResult Recipe;
    public float NextCraftingTime;
    public BlockFacing? Facing;
    public int Amount;
}

// We don't use the DummySlot that comes with VanillaAPI, because the game assumes it is only used by handbook.
internal class DummySlot : ItemSlot
{
    public DummySlot(ItemStack stack) : base(null)
    {
        base.Itemstack = stack;
        this.MarkDirty();
    }
}