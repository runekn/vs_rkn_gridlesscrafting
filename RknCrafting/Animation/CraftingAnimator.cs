using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RKN.Crafting.Animation;

public class CraftingAnimator
{
    private ICoreAPI api;

    public CraftingAnimator(ICoreAPI api)
    {
        this.api = api;
    }

    private ICoreClientAPI CoreClientAPI { get { return api as ICoreClientAPI; } }

    private static string ToAnimationCode(EnumCraftingAnimation state) => state switch
    {
        // breaktool
        // axehit
        // knifescrape
        // knifecut
        // chiselhit
        EnumCraftingAnimation.HandsGeneric => "rkncrafting.handsmixing",
        EnumCraftingAnimation.HandsTool => "breakhand", // TODO
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

    public EnumCraftingAnimation StartCrafting(IPlayer byPlayer, int recipe, ItemSlot? primaryTool, ItemSlot? offhandTool)
    {
        EnumCraftingAnimation animation = GetCraftingAnimation(recipe, primaryTool, offhandTool);
        string anim = ToAnimationCode(animation);
        if (!byPlayer.Entity.AnimManager.StartAnimation(anim))
        {
            api.RCLogger().Warning("Could not start animation: " + anim);
        }
        return animation;
    }

    public void StopCrafting(IPlayer byPlayer, EnumCraftingAnimation animation)
    {
        string anim = ToAnimationCode(animation);
        byPlayer.Entity.AnimManager.StopAnimation(anim);
    }

    private EnumCraftingAnimation GetCraftingAnimation(int recipe, ItemSlot? primaryTool, ItemSlot? offhandTool)
    {
        if (primaryTool == null)
        {
            if (api.RCRecipeCatalog().GetRecipeById(recipe).Output?.ResolvedItemStack?.Item?.Tool != null)
            {
                return EnumCraftingAnimation.HandsTool;
            }
            return EnumCraftingAnimation.HandsGeneric;
        }
        EnumTool? primary = primaryTool?.Itemstack?.Item?.Tool;
        EnumTool? offhand = offhandTool?.Itemstack?.Item?.Tool;
        return primary switch
        {
            EnumTool.Knife => EnumCraftingAnimation.Knife,
            EnumTool.Axe => offhand == EnumTool.Hammer ? EnumCraftingAnimation.AxeHammer : EnumCraftingAnimation.Axe,
            EnumTool.Hammer => EnumCraftingAnimation.Hammer,
            EnumTool.Shears => EnumCraftingAnimation.Shears,
            EnumTool.Saw => EnumCraftingAnimation.Saw,
            EnumTool.Chisel => offhand == EnumTool.Hammer ? EnumCraftingAnimation.ChiselHammer : EnumCraftingAnimation.Chisel,
            EnumTool.Club => EnumCraftingAnimation.Club,
            _ => EnumCraftingAnimation.HandsGeneric
        };

    }
}

public enum EnumCraftingAnimation
{
    HandsTool,
    HandsGeneric,
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
