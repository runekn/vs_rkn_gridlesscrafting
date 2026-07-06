using RKN.GridlessCrafting.Entities;
using RKN.GridlessCrafting.Network;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using GridlessCrafting;

namespace RKN.GridlessCrafting;

public class GridlessCraftingModSystem : ModSystem
{
#pragma warning disable CS8618
    private ICoreAPI api;
    private Harmony harmony;

    public GridlessCraftingNetwork Network { get; internal set; }
    public RecipeCatalog RecipeCatalog { get; internal set; }
#pragma warning restore CS8618

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        this.api = api;
        api.RegisterBlockClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockCraftingSurface));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockEntityCraftingSurface));
        api.RegisterBlockBehaviorClass(Mod.Info.ModID + ".spawncraftingsurface", typeof(BlockBehaviorSpawnCraftingSurface));
        api.RegisterCollectibleBehaviorClass(Mod.Info.ModID + ".spawncraftingsurface", typeof(CollectibleBehaviorSpawnCraftingSurface));
        harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();
        api.GCLogger().Debug("Hello world!");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.LevelFinalize += InitCatalog;
        api.Input.RegisterHotKey("rkngridlesscrafting.start", Lang.Get("hotkey-crafting"), GlKeys.AltLeft);
        Network = new GridlessCraftingNetwork(api, Mod.Info.ModID);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        InitCatalog();
        Network = new GridlessCraftingNetwork(api, Mod.Info.ModID);
    }

    public override void Dispose()
    {
        harmony.UnpatchAll(Mod.Info.ModID);
    }

    /*public override void AssetsFinalize(ICoreAPI api)
    {
        foreach (CollectibleObject collectible in api.World.Collectibles)
        {
            if (collectible.Code == null ||
                collectible.Id == 0 ||
                (collectible.ItemClass != EnumItemClass.Item && collectible.ItemClass != EnumItemClass.Block) || 
                (collectible is Item item && item.Tool != null) || 
                collectible.HasBehavior<CollectibleBehaviorSpawnCraftingSurface>())
            {
                continue;
            }
            CollectibleBehaviorSpawnCraftingSurface instance = new CollectibleBehaviorSpawnCraftingSurface(collectible);
            collectible.CollectibleBehaviors.Append(instance); // TODO: this isn't working...
        }
    }*/

    public void InitCatalog()
    {
        RecipeCatalog = new RecipeCatalog(api);
    }
}
