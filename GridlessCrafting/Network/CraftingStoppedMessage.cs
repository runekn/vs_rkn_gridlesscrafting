using ProtoBuf;

namespace RKN.GridlessCrafting.Network;

[ProtoContract]
public class CraftingStoppedMessage
{
    [ProtoMember(1)]
    public required EnumCraftingAnimation animation;
}