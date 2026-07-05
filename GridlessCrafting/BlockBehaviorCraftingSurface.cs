using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RKN.GridlessCrafting;

public class BlockBehaviorCraftingSurface : BlockBehavior
{
    public BlockBehaviorCraftingSurface(Block block) : base(block)
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
        clientApi.Network.GetChannel("rkngridlesscrafting").SendPacket(new CreateCraftingBlockMessage() { Position = blockSel.Position });
        handling = EnumHandling.PreventSubsequent;
        return false; // Prevent server message as we will do that ourself
    }

    public void TryPlaceCrafting(IWorldAccessor world, IPlayer byPlayer, BlockPos blockPos)
    {
        (world.GetBlock(new AssetLocation("rkngridlesscrafting:crafting")) as BlockCrafting).TryPlace(byPlayer, blockPos, byPlayer.InventoryManager.ActiveHotbarSlot);
    }
}