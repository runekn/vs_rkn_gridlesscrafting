using GridlessCrafting;
using RKN.GridlessCrafting.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RKN.GridlessCrafting.Entities;

public class BlockCraftingSurface : Block
{

    public static bool TryPlace(ICoreAPI api, IPlayer byPlayer, BlockPos blockPos, ItemSlot slot)
    {
        BlockCraftingSurface? block = api.World.GetBlock(new AssetLocation("rkngridlesscrafting:craftingsurface")) as BlockCraftingSurface;
        if (block == null)
        {
            api.GCLogger().Error("Crafting block did not spawn with BlockEntityCraftingSurface!");
            return false;
        }
        api.GCLogger().Debug("Trying to place crafting block at " + blockPos.ToString());
        BlockPos abovePos = blockPos.UpCopy(1);
        if (api.World.BlockAccessor.GetBlock(abovePos).Replaceable < 6000)
        {
            return false;
        }
        api.World.BlockAccessor.SetBlock(block.Id, abovePos);
        BlockEntityCraftingSurface? blockEntity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(abovePos);
        if (blockEntity == null)
        {
            api.GCLogger().Error("Crafting block did not spawn with BlockEntityCraftingSurface!");
            api.World.BlockAccessor.BreakBlock(abovePos, null);
            return false;
        }
        if (!blockEntity.TryPutIngredient(byPlayer.InventoryManager.ActiveHotbarSlot, byPlayer))
        {
            api.GCLogger().Error("Could not put initial items into newly spawned crafting block!");
            api.World.BlockAccessor.BreakBlock(abovePos, null);
            return false;
        }
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCraftingSurface? be = GetBE(world, blockSel.Position);
        if (be == null)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        if (api.Side == EnumAppSide.Client && (api as ICoreClientAPI).Input.IsHoldingCraftingButton())
        {
            api.GCNetwork().SelectNextRecipe(blockSel.Position);
            return false;
        }
        ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeHotbarSlot.Empty || activeHotbarSlot.Itemstack?.Item?.Tool != null)
        {
            HandlePlayerAnimation(byPlayer, be.StartCrafting(world, byPlayer, this));
        } else {
            be.TryPutIngredient(activeHotbarSlot, byPlayer);
        }
        return true;
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
            HandlePlayerAnimation(byPlayer, be.OnCraftingStep(secondsUsed, world, byPlayer, blockSel));
        }
        return true;
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
            HandlePlayerAnimation(byPlayer, be.CancelCrafting(world, byPlayer, blockSel));
        }
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        BlockPos lowerNeightbor = pos.DownCopy();
        if (lowerNeightbor.Equals(neibpos) && world.BlockAccessor.GetBlock(lowerNeightbor).Id == 0)
        {
            world.BlockAccessor.BreakBlock(pos, null);
        }
        base.OnNeighbourBlockChange(world, pos, neibpos);
    }

    private void HandlePlayerAnimation(IPlayer byPlayer, PlayerAnimationRequest? request)
    {
        if (request == null)
        {
            return;
        }
        string anim = PlayerAnimationRequest.ToAnimationCode(request.Animation);
        api.GCLogger().Debug("Animation change {0} {1} ", [request.Action, anim]);
        if (request.Action == EnumAnimationAction.START)
        {
            byPlayer.Entity.StartAnimation(anim);
        }
        else
        {
            byPlayer.Entity.StopAnimation(anim);
        }
    }

    private static BlockEntityCraftingSurface? GetBE(IWorldAccessor world, BlockPos blockPos)
    {
        if (blockPos == null)
        {
            return null;
        }
        return world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityCraftingSurface;
    }
}