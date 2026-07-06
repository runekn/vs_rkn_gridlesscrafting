using RKN.GridlessCrafting;
using RKN.GridlessCrafting.Network;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace GridlessCrafting;

public static class CoreApiExtensions
{
    public static RecipeCatalog GCRecipeCatalog(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<GridlessCraftingModSystem>().RecipeCatalog;
    }

    public static GridlessCraftingNetwork GCNetwork(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<GridlessCraftingModSystem>().Network;
    }

    public static ILogger GCLogger(this ICoreAPI api)
    {
        return api.ModLoader.GetMod("rkngridlesscrafting").Logger;
    }

    public static bool IsHoldingCraftingButton(this IInputAPI api)
    {
        return api.IsHotKeyPressed("rkngridlesscrafting.start");
    }
}
