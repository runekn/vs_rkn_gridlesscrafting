using RKN.GridlessCrafting;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace GridlessCrafting;

public class CollectibleBehaviorSpawnCraftingSurface : CollectibleBehavior
{
    public CollectibleBehaviorSpawnCraftingSurface(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (blockSel.Face != BlockFacing.UP)
        {
            return;
        }
        ICoreClientAPI? clientApi = byEntity.World.Api as ICoreClientAPI;
        if (clientApi == null || !clientApi.Input.IsHotKeyPressed("rkngridlesscrafting.start") || slot?.Itemstack?.Item?.Tool != null)
        {
            return;
        }
        if (byEntity is not EntityPlayer player)
        {
            return;
        }
        bool r = (byEntity.World.GetBlock(new AssetLocation("rkngridlesscrafting:craftingsurface")) as BlockCrafting).TryPlace(player.Player, blockSel.Position, slot);
        if (!r)
        {
            return;
        }
        clientApi.Network.GetChannel("rkngridlesscrafting").SendPacket(new CreateCraftingBlockMessage() { Position = blockSel.Position });
        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefaultAction; // TODO: does not prevent default placing of block, which causes crash since block has already been moved to crafting surface.
    }
}
