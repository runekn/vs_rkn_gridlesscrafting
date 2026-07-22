using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RknCrafting.Entities;

public class ItemUnfinishedCraft : Item
{
    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        dsc.Append("Used tools: ");
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        ItemStack outputStack = itemStack.Attributes.GetItemstack("output");
        if (outputStack == null)
        {
            return "ERROR: Unknown output";
        }
        outputStack.ResolveBlockOrItem(api.World);
        return "Unfinished " + outputStack.GetName();
    }
}