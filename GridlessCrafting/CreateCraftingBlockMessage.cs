using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RKN.GridlessCrafting;

[ProtoContract]
public class CreateCraftingBlockMessage
{
    [ProtoMember(1)]
    public required BlockPos Position;
}