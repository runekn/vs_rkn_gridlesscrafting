using System;

namespace RKN.Crafting;

public class PlayerAnimationRequest : Tuple<EnumCraftingAnimation, EnumAnimationAction>
{
    public EnumCraftingAnimation Animation => Item1;

    public EnumAnimationAction Action => Item2;

    public PlayerAnimationRequest(EnumCraftingAnimation item1, EnumAnimationAction item2) : base(item1, item2)
    {
    }

    public static string ToAnimationCode(EnumCraftingAnimation state) => state switch
    {
        // breaktool
        // axehit
        // knifescrape
        // knifecut
        // chiselhit
        EnumCraftingAnimation.HandsMixing    => "rkncrafting.handsmixing",
        EnumCraftingAnimation.HandsTool    => "breakhand", // TODO
        EnumCraftingAnimation.Hammer => "hammerhit",
        EnumCraftingAnimation.Axe => "axechop",
        EnumCraftingAnimation.AxeHammer => "rkncrafting.axehammer",
        EnumCraftingAnimation.Saw => "saw",
        EnumCraftingAnimation.Shears => "shears",
        EnumCraftingAnimation.ChiselHammer => "hammerandchisel",
        EnumCraftingAnimation.Chisel => "hammerandchisel", // TODO
        EnumCraftingAnimation.Knife => "knifecut",
        EnumCraftingAnimation.Club => "hammerhit", // TODO
        _ => throw new ArgumentOutOfRangeException(nameof(state), $"Not expected animation value: {state}"),
    };
}

public enum EnumCraftingAnimation
{
    HandsTool,
    HandsMixing,
    Hammer,
    Chisel,
    ChiselHammer,
    Axe,
    AxeHammer,
    Knife,
    Shears,
    Saw,
    Club,
}

public enum EnumAnimationAction
{
    START, STOP
}