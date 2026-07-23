using RKN.Crafting.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RknCrafting.Entities;

public class BlockBehaviorChiselSpawnCraftingSurface(Block block) : BlockBehaviorSpawnCraftingSurface(block)
{
    public override float GetCraftingModifier(IWorldAccessor world, BlockSelection blockSel)
    {
        BlockEntityMicroBlock entity = world.BlockAccessor.GetBlockEntity<BlockEntityMicroBlock>(blockSel.Position);
        return world.GetBlock(entity.BlockIds[0]).GetBehavior<BlockBehaviorSpawnCraftingSurface>()?.GetCraftingModifier(world, blockSel) ?? 1f;
    }
}