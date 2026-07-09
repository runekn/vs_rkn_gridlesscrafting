using HarmonyLib;
using RKN.Crafting.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace RknCrafting.Patches;

[HarmonyPatch(typeof(SystemRenderDecals), "AddBlockBreakDecal")]
public class SystemRenderDecalsPatch
{
    static bool Prefix(BlockPos pos, int stage, ref object __result, ref ClientMain ___game)
    {
        if (___game.api.World.BlockAccessor.GetBlock(pos) is BlockCraftingSurface)
        {
            __result = null;
            return false;
        }
        return true;
    }
}
