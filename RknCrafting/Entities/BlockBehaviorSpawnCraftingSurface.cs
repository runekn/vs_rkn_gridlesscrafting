using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RKN.Crafting.Entities;

public class BlockBehaviorSpawnCraftingSurface(Block block) : BlockBehavior(block)
{
    private float craftingTimeModifier = 1.0f;
    public float CraftingTimeModifier { get { return craftingTimeModifier; } }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        craftingTimeModifier = properties["craftingTimeModifier"].AsFloat(1.0f);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (blockSel.Face != BlockFacing.UP || !block.SideIsSolid(blockSel.Position, BlockFacing.indexUP))
        {
            return true;
        }
        ICoreClientAPI? clientApi = world.Api as ICoreClientAPI;
        if (clientApi == null || !clientApi.Input.IsHoldingCraftingButton() || byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item?.Tool != null)
        {
            return true;
        }
        bool r = BlockCraftingSurface.TryPlace(world.Api, byPlayer, blockSel.Position, byPlayer.InventoryManager.ActiveHotbarSlot);
        if (!r) {
            return true;
        }
        world.Api.RCNetwork().SpawnCraftingSurface(blockSel.Position);
        handling = EnumHandling.PreventSubsequent;
        // It would be better if I could return false to prevent default server message as we have done that ourselves.
        // But that will also enable default behavior to place block, which causes game to crash because we have now removed the block from inventory.
        return true; 
    }
}