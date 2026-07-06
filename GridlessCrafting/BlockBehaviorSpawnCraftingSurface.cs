using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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
        // TODO: Causes crash when spawning with block as initial ingredient. Because default action is not being prevented, and it when fails with NPE because block is already removed.
        /*bool r = (world.GetBlock(new AssetLocation("rkngridlesscrafting:craftingsurface")) as BlockCrafting).TryPlace(byPlayer, blockSel.Position, byPlayer.InventoryManager.ActiveHotbarSlot);
        if (!r) {
            return true;
        }*/
        clientApi.Network.GetChannel("rkngridlesscrafting").SendPacket(new CreateCraftingBlockMessage() { Position = blockSel.Position });
        handling = EnumHandling.PreventSubsequent;
        return false; // Prevent default server message as we have done that ourselves
    }
}