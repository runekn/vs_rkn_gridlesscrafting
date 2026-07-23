using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RKN.Crafting.Entities;

public class BlockBehaviorSpawnCraftingSurface(Block block) : BlockBehavior(block)
{
    private float craftingTimeModifier = 1.0f;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        craftingTimeModifier = properties["craftingTimeModifier"].AsFloat(1.0f);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (blockSel.Face != BlockFacing.UP)
        {
            return true;
        }
        if (!block.SideIsSolid(blockSel.Position, BlockFacing.indexUP))
        {
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "rkncrafting.unsuitablesurface", Lang.Get("rkncrafting:error-unsuitablesurface"));
            return true;
        }
        ICoreClientAPI? clientApi = world.Api as ICoreClientAPI;
        if (clientApi == null || !clientApi.Input.IsHoldingCraftingButton())
        {
            return true;
        }
        bool r = BlockCraftingSurface.TryPlace(world.Api, byPlayer, blockSel.Position, byPlayer.InventoryManager.ActiveHotbarSlot, GetCraftingModifier(world, blockSel));
        if (!r) {
            return true;
        }
        world.Api.RcNetwork().SpawnCraftingSurface(blockSel.Position);
        handling = EnumHandling.PreventSubsequent;
        // It would be better if I could return false to prevent default server message as we have done that ourselves.
        // But that will also enable default behavior to place block, which causes game to crash because we have now removed the block from inventory.
        return true; 
    }

    public virtual float GetCraftingModifier(IWorldAccessor world, BlockSelection blockSel)
    {
        return craftingTimeModifier;
    }
}