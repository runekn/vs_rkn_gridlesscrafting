using RKN.Crafting;
using RKN.Crafting.Animation;
using RKN.Crafting.Network;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RKN.Crafting;

public static class CoreApiExtensions
{
    public static RecipeCatalog RCRecipeCatalog(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().RecipeCatalog;
    }

    public static RknCraftingNetwork RCNetwork(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().Network;
    }

    public static CraftingAnimator RCAnimator(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().Animator;
    }

    public static ILogger RCLogger(this ICoreAPI api)
    {
        return api.ModLoader.GetMod("rkncrafting").Logger;
    }

    public static bool IsHoldingCraftingButton(this IInputAPI api)
    {
        return api.IsHotKeyPressed("rkncrafting.start");
    }
}
