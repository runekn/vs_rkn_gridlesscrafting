using ProtoBuf;
using RKN.Crafting.Animation;
using Vintagestory.API.MathTools;

namespace RKN.Crafting.Network;

[ProtoContract]
public class CreateCraftingBlockMessage
{
    [ProtoMember(1)]
    public required BlockPos Position;
}

[ProtoContract]
public class CraftingStoppedMessage
{
    [ProtoMember(1)]
    public required EnumCraftingAnimation animation;
}

[ProtoContract]
public class SelectNextRecipeMessage
{
    [ProtoMember(1)]
    public required BlockPos Position;
}

[ProtoContract]
public class RecipeConsumedMessage
{
    [ProtoMember(1)]
    public required BlockPos Position;
}