using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace RKN.GridlessCrafting;

public class BlockBehaviorSpawnCraftingSurface : BlockBehavior
{
    public BlockBehaviorSpawnCraftingSurface(Block block) : base(block)
    {
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (blockSel.Face != BlockFacing.UP)
        {
            return true;
        }
        ICoreClientAPI? clientApi = world.Api as ICoreClientAPI;
        if (clientApi == null || !clientApi.Input.IsHotKeyPressed("rkngridlesscrafting.start") || byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item?.Tool != null)
        {
            return true;
        }
        bool r = (world.GetBlock(new AssetLocation("rkngridlesscrafting:craftingsurface")) as BlockCrafting).TryPlace(byPlayer, blockSel.Position, byPlayer.InventoryManager.ActiveHotbarSlot);
        if (!r) {
            return true;
        }
        clientApi.Network.GetChannel("rkngridlesscrafting").SendPacket(new CreateCraftingBlockMessage() { Position = blockSel.Position });
        handling = EnumHandling.PreventSubsequent;
        // It would be better if I could return false to prevent default server message as we have done that ourselves.
        // But that will also enable default behavior to place block, which causes game to crash because we have now removed the block from inventory.
        return true; 
    }
}