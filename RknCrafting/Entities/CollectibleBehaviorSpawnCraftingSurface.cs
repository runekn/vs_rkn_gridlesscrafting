using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RKN.Crafting.Entities;

public class CollectibleBehaviorSpawnCraftingSurface(CollectibleObject collObj) : CollectibleBehavior(collObj)
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (blockSel.Face != BlockFacing.UP)
        {
            return;
        }
        ICoreClientAPI? clientApi = byEntity.World.Api as ICoreClientAPI;
        if (clientApi == null || !clientApi.Input.IsHoldingCraftingButton() || slot?.Itemstack?.Item?.Tool != null)
        {
            return;
        }
        if (byEntity is not EntityPlayer player)
        {
            return;
        }
        bool r = BlockCraftingSurface.TryPlace(byEntity.World.Api, player.Player, blockSel.Position, slot);
        if (!r)
        {
            return;
        }
        byEntity.World.Api.RcNetwork().SpawnCraftingSurface(blockSel.Position);
        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefaultAction; // TODO: does not prevent default placing of block, which causes crash since block has already been moved to crafting surface.
    }
}
