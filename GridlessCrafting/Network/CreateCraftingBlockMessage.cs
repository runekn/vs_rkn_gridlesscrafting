using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RKN.GridlessCrafting.Network;

[ProtoContract]
public class CreateCraftingBlockMessage
{
    [ProtoMember(1)]
    public required BlockPos Position;
}