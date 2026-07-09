using HarmonyLib;
using RKN.Crafting.Entities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace RknCrafting.Patches;

[HarmonyPatch(typeof(SystemRenderDecals), "AddBlockBreakDecal")]
public class SystemRenderDecalsPatch
{
    static bool Prefix(BlockPos pos, int stage, ref object __result, ref ClientMain ___game, SystemRenderDecals __instance)
    {
        if (___game.api.World.BlockAccessor.GetBlock(pos) is BlockCraftingSurface)
        {
            __result = null;
            return false;
        }
        return true;
    }
}
