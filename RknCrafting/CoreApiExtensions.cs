using RKN.Crafting;
using RKN.Crafting.Animation;
using RKN.Crafting.Network;
using RknCrafting;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RKN.Crafting;

public static class CoreApiExtensions
{
    public static RecipeCatalog RcRecipeCatalog(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().RecipeCatalog;
    }

    public static RknCraftingNetwork RcNetwork(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().Network;
    }

    public static CraftingAnimator RcAnimator(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().Animator;
    }

    public static RknCraftingConfig RcConfig(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>().Config;
    }

    public static RknCraftingModSystem RcSystem(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<RknCraftingModSystem>();
    }

    public static ILogger RcLogger(this ICoreAPI api)
    {
        return api.ModLoader.GetMod("rkncrafting").Logger;
    }

    public static bool IsHoldingCraftingButton(this IInputAPI api)
    {
        return api.IsHotKeyPressed("rkncrafting.start");
    }

    public static void RcPauseInteractions(this ICoreAPI api)
    {
        api.ModLoader.GetModSystem<RknCraftingModSystem>().BeginPauseInterations = Environment.TickCount;
    }
}
