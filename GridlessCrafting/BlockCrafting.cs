using System;
using System.Data;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RKN.GridlessCrafting;

public class BlockCrafting : Block
{

    public void TryPlace(IPlayer byPlayer, BlockPos blockPos, ItemSlot slot)
    {
        api.Logger.Debug("[gridlesscrafting] Trying to place crafting block at " + blockPos.ToString());
        BlockPos abovePos = blockPos.UpCopy(1);
        if (api.World.BlockAccessor.GetBlock(abovePos).Replaceable < 6000)
        {
            return;
        }
        api.World.BlockAccessor.SetBlock(Id, abovePos);
        BlockEntityCrafting? blockEntity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCrafting>(abovePos);
        if (blockEntity == null)
        {
            api.Logger.Error("[rkngridlesscrafting] Crafting block did not spawn with BlockEntityCrafting!");
            api.World.BlockAccessor.BreakBlock(abovePos, null);
            return;
        }
        if (!blockEntity.TryPut(byPlayer.InventoryManager.ActiveHotbarSlot, byPlayer))
        {
            api.Logger.Error("[rkngridlesscrafting] Could not put initial items into newly spawned crafting block!");
            api.World.BlockAccessor.BreakBlock(abovePos, null);
            return;
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCrafting? be = GetBE(world, blockSel);
        if (be == null)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeHotbarSlot.Empty || activeHotbarSlot.Itemstack?.Item?.Tool != null)
        {
            HandlePlayerAnimation(byPlayer, be.StartCrafting(world, byPlayer, this));
        } else {
            be.TryPut(activeHotbarSlot, byPlayer);
        }
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityCrafting? be = GetBE(world, blockSel);
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
        BlockEntityCrafting? be = GetBE(world, blockSel);
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

    private void HandlePlayerAnimation(IPlayer byPlayer, PlayerAnimationRequest? request)
    {
        if (request == null)
        {
            return;
        }
        string anim = PlayerAnimationRequest.ToAnimationCode(request.Animation);
        api.Logger.Debug("[gridlesscrafting] Animation change {0} {1} ", [request.Action, anim]);
        if (request.Action == EnumAnimationAction.START)
        {
            byPlayer.Entity.StartAnimation(anim);
        }
        else
        {
            byPlayer.Entity.StopAnimation(anim);
        }
    }

    private static BlockEntityCrafting? GetBE(IWorldAccessor world, BlockSelection blockSel)
    {
        if (blockSel == null)
        {
            return null;
        }
        return world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrafting;
    }
}