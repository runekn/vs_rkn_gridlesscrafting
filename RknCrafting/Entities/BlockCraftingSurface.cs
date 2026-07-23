using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RKN.Crafting.Entities;

public class BlockCraftingSurface : Block
{
    private static readonly AssetLocation Asset = new("rkncrafting", "craftingsurface");

    private WorldInteraction[] interactions;

    public static bool TryPlace(ICoreAPI api, IPlayer? byPlayer, BlockPos blockPos, ItemSlot slot, float craftingTimeModifier = 1f)
    {
        if (api.World.GetBlock(Asset) is not BlockCraftingSurface block)
        {
            api.RcLogger().Error("Crafting block did not spawn with BlockEntityCraftingSurface!");
            return false;
        }
        api.RcLogger().Debug("Trying to place crafting block at " + blockPos);
        BlockPos abovePos = blockPos.UpCopy(1);
        if (api.World.BlockAccessor.GetBlock(abovePos).Replaceable < 6000)
        {
            return false;
        }
        api.World.BlockAccessor.SetBlock(block.Id, abovePos);
        BlockEntityCraftingSurface? blockEntity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(abovePos);
        if (blockEntity == null)
        {
            api.RcLogger().Error("Crafting block did not spawn with BlockEntityCraftingSurface!");
            api.World.BlockAccessor.BreakBlock(abovePos, null);
            return false;
        }
        blockEntity.CraftingSurfaceTimeModifier = craftingTimeModifier;
        if (api.RcServerConfig().EnableGridless)
        {
            if (slot.Itemstack?.Item?.Tool == null && !blockEntity.TryPutIngredient(slot, byPlayer))
            {
                api.RcLogger().Warning("Could not put initial items into newly spawned crafting block!");
                return false;
            }
        }
        return true;
    }

    public override void OnLoaded(ICoreAPI api)
    {
        PlacedPriorityInteract = true;
        InteractionHelpYOffset = 0.6f;
        interactions =
        [
            new WorldInteraction
            {
                ActionLangCode = "rkncrafting:help-craft",
                MouseButton = EnumMouseButton.Right,
                ShouldApply = IsCraftable
            },
            new WorldInteraction
            {
                ActionLangCode = "rkncrafting:help-craftbulk",
                HotKeyCode = "shift",
                MouseButton = EnumMouseButton.Right,
                ShouldApply = (wi, blockSel, entitySel) => api.RcServerConfig().EnableBulkCrafting && IsCraftable(wi, blockSel, entitySel)
            },
            new WorldInteraction
            {
                ActionLangCode = "rkncrafting:help-addingredient",
                MouseButton = EnumMouseButton.Right,
                ShouldApply = CanAddIngredient
            },
            new WorldInteraction
            {
                ActionLangCode = "rkncrafting:help-takeingredient",
                HotKeyCode = "ctrl",
                MouseButton = EnumMouseButton.Right,
                ShouldApply = CanTakeIngredient
            },
            new WorldInteraction
            {
                ActionLangCode = "rkncrafting:help-addtoolingredient",
                HotKeyCode = "rkncrafting.start",
                MouseButton = EnumMouseButton.Right,
                ShouldApply = CanAddToolIngredient
            },
            new WorldInteraction
            {
                ActionLangCode = "Select recipe",
                HotKeyCode = "toolmodeselect",
                MouseButton = EnumMouseButton.None,
                ShouldApply = CanSelectRecipe
            }
        ];
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (api.RcServerConfig().EnableGridless)
        {
            return base.GetSelectionBoxes(blockAccessor, pos);
        }
        float thickness = 0.075f;
        return [
            new Cuboidf(0,      0,  0,      1/3f,   thickness,   1/3f),
            new Cuboidf(1/3f,   0,  0,      2/3f,   thickness,   1/3f),
            new Cuboidf(2/3f,   0,  0,      1f,     thickness,   1/3f),
            new Cuboidf(0,      0,  1/3f,   1/3f,   thickness,   2/3f),
            new Cuboidf(1/3f,   0,  1/3f,   2/3f,   thickness,   2/3f),
            new Cuboidf(2/3f,   0,  1/3f,   1f,     thickness,   2/3f),
            new Cuboidf(0,      0,  2/3f,   1/3f,   thickness,   1f),
            new Cuboidf(1/3f,   0,  2/3f,   2/3f,   thickness,   1f),
            new Cuboidf(2/3f,   0,  2/3f,   1f,     thickness,   1f),
        ];
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCraftingSurface? be = GetBE(world, blockSel.Position);
        if (be == null)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeHotbarSlot.Empty)
        {
            if (byPlayer.Entity.Controls.CtrlKey)
            {
                return be.TryTakeIngredient(activeHotbarSlot, byPlayer, blockSel.SelectionBoxIndex);
            }
            return be.StartCrafting(byPlayer);
        }
        if (activeHotbarSlot.Itemstack?.Item?.Tool != null)
        {
            if ((api as ICoreClientAPI)?.Input.IsHoldingCraftingButton() ?? false)
            {
                if (be.TryPutIngredient(activeHotbarSlot, byPlayer, blockSel.SelectionBoxIndex))
                {
                    api.RcNetwork().PutToolIngredient(blockSel);
                }
                return false;
            }
            return be.StartCrafting(byPlayer);
        }
        if (byPlayer.Entity.Controls.CtrlKey)
        {
            return be.TryTakeIngredient(activeHotbarSlot, byPlayer, blockSel.SelectionBoxIndex);
        }
        return be.TryPutIngredient(activeHotbarSlot, byPlayer, blockSel.SelectionBoxIndex);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCraftingSurface? be = GetBE(world, blockSel.Position);
        if (be == null)
        {
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }
        if (be.IsCrafting(byPlayer))
        {
            return be.OnCraftingStep(secondsUsed, byPlayer);
        }
        return false;
    }
    
    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCraftingSurface? be = GetBE(world, blockSel.Position);
        if (be == null)
        {
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
            return;
        }
        if (be.IsCrafting(byPlayer))
        {
            be.CancelCrafting(byPlayer);
        }
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        BlockEntityCraftingSurface? be = GetBE(world, blockSel.Position);
        if (be == null)
        {
            base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
            return true;
        }
        if (be.IsCrafting(byPlayer))
        {
            be.CancelCrafting(byPlayer);
        }
        return true;
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        // Break if lower neighbor is broken
        BlockPos lowerNeightbor = pos.DownCopy();
        if (lowerNeightbor.Equals(neibpos) && world.BlockAccessor.GetBlock(lowerNeightbor).Id == 0)
        {
            world.BlockAccessor.BreakBlock(pos, null);
        }
        base.OnNeighbourBlockChange(world, pos, neibpos);
    }

    public override void OnBeingLookedAt(IPlayer byPlayer, BlockSelection blockSel, bool firstTick)
    {
        base.OnBeingLookedAt(byPlayer, blockSel, firstTick);
        BlockEntityCraftingSurface? be = GetBE(api.World, blockSel.Position);
        if (be != null)
        {
            be.CheckIfUpdateRecipes(byPlayer);
        }
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, float dropQuantityMultiplier = 1)
    {
        // Copy paste of base method, but without behavior delegation and spawning particles
        if (EntityClass != null)
        {
            world.BlockAccessor.GetBlockEntity(pos)?.OnBlockBroken(byPlayer);
        }
        if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
        {
            ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            if (drops != null)
            {
                for (int i = 0; i < drops.Length; i++)
                {
                    if (SplitDropStacks)
                    {
                        for (int k = 0; k < drops[i].StackSize; k++)
                        {
                            ItemStack stack = drops[i].Clone();
                            stack.StackSize = 1;
                            world.SpawnItemEntity(stack, pos);
                        }
                    }
                    else
                    {
                        world.SpawnItemEntity(drops[i].Clone(), pos);
                    }
                }
            }
            if (Sounds != null)
            {
                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, 0.0, byPlayer);
            }
        }
        world.BlockAccessor.SetBlock(0, pos);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions;
    }

    private bool IsCraftable(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
    {
        ItemSlot activeSlot = (api as ICoreClientAPI)!.World.Player.InventoryManager.ActiveHotbarSlot;
        if (!activeSlot.Empty && activeSlot.Itemstack.Item?.Tool == null)
        {
            return false;
        }
        BlockEntityCraftingSurface? be = GetBE(api.World, blockSelection.Position);
        return be != null && be.HasRecipeSelection();
    }

    private bool CanSelectRecipe(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
    {
        BlockEntityCraftingSurface? be = GetBE(api.World, blockSelection.Position);
        return be != null && be.HasRecipeSelection();
    }

    private bool CanAddToolIngredient(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
    {
        ItemSlot activeSlot = (api as ICoreClientAPI)!.World.Player.InventoryManager.ActiveHotbarSlot;
        return !activeSlot.Empty && activeSlot.Itemstack?.Item?.Tool != null; 
    }

    private bool CanAddIngredient(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
    {
        ItemSlot activeSlot = (api as ICoreClientAPI)!.World.Player.InventoryManager.ActiveHotbarSlot;
        return !activeSlot.Empty && activeSlot.Itemstack?.Item?.Tool == null; 
    }

    private bool CanTakeIngredient(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
    {
        BlockEntityCraftingSurface? be = GetBE(api.World, blockSelection.Position);
        return be != null && !be.IsEmpty();
    }

    public static BlockEntityCraftingSurface? GetBE(IWorldAccessor world, BlockPos blockPos)
    {
        return world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityCraftingSurface;
    }
}