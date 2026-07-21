using HarmonyLib;
using RKN.Crafting.Animation;
using RKN.Crafting.Entities;
using RKN.Crafting.Network;
using RKN.Crafting.Patches;
using RknCrafting;
using System;
using System.Formats.Asn1;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace RKN.Crafting;

public class RknCraftingModSystem : ModSystem
{
#pragma warning disable CS8618
    private ICoreAPI api;
    private Harmony harmony;

    public RknCraftingNetwork Network { get; internal set; }
    public RecipeCatalog RecipeCatalog { get; internal set; }
    public CraftingAnimator Animator { get; internal set; }
    public RknCraftingConfig Config { get; internal set; }
    public long BeginPauseInterations { get; set; }
#pragma warning restore CS8618

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        this.api = api;
        api.RegisterBlockClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockCraftingSurface));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockEntityCraftingSurface));
        api.RegisterBlockBehaviorClass(Mod.Info.ModID + ".spawncraftingsurface", typeof(BlockBehaviorSpawnCraftingSurface));
        api.RegisterCollectibleBehaviorClass(Mod.Info.ModID + ".spawncraftingsurface", typeof(CollectibleBehaviorSpawnCraftingSurface));
        Animator = new CraftingAnimator(api);
        ApplyHarmonyPatches();

        api.RcLogger().Debug("Hello world!");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        //api.Event.LevelFinalize += InitCatalog;
        api.Input.RegisterHotKey("rkncrafting.start", Lang.Get("rkncrafting:hotkey-crafting"), GlKeys.AltLeft);
        Network = new RknCraftingNetwork(api, Mod.Info.ModID);
        api.Event.BlockChanged += UpdateCraftingSurface; // Why is this neccessary? Vanilla shelf seems to work just fine without.
        api.Input.InWorldAction += CheckPauseInteractions;
        api.Event.MouseUp += CheckResumeInteractions;
    }

    private void CheckResumeInteractions(MouseEvent e)
    {
        if (e.Button == EnumMouseButton.Right)
        {
            BeginPauseInterations = 0;
        }
    }

    private void CheckPauseInteractions(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (action == EnumEntityAction.InWorldRightMouseDown && (Environment.TickCount - BeginPauseInterations) < 2_000)
        {
            handled = EnumHandling.PreventDefault;
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        TryLoadConfig();
        InitCatalog();
        Network = new RknCraftingNetwork(api, Mod.Info.ModID);
        api.Event.PlayerJoin += SendConfig;

        api.ChatCommands.Create("addcraft")
            .WithDescription("Spawn crafting surface with held item, without player replication. For testing.")
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .HandleWith((args) =>
            {
                IPlayer byPlayer = args.Caller.Player;
                BlockPos position = byPlayer.CurrentBlockSelection.Position;
                if (api.World.BlockAccessor.GetBlock(position) is BlockCraftingSurface)
                {
                    api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(position).TryPutIngredient(byPlayer.InventoryManager.ActiveHotbarSlot);
                }
                else
                {
                    if (!BlockCraftingSurface.TryPlace(api, null, position, byPlayer.InventoryManager.ActiveHotbarSlot))
                    {
                        return TextCommandResult.Error("Could not place crafting surface there");
                    }
                }
                return TextCommandResult.Success();
            });
    }

    private void SendConfig(IServerPlayer byPlayer)
    {
        Network.TransferConfig(Config, byPlayer);
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

    private void UpdateCraftingSurface(BlockPos pos, Block oldBlock)
    {
        if (oldBlock is BlockCraftingSurface)
        {
            BlockEntityCraftingSurface.OnInventoryUpdated(api as ICoreClientAPI, pos);
        }
    }

    private void ApplyHarmonyPatches()
    {
        // Use local config instead of server supplied one
        RknCraftingConfig config = api.LoadModConfig<RknCraftingConfig>(Mod.Info.ModID + ".json");
        if (config == null)
        {
            config = new RknCraftingConfig();
        }

        harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();

        if (config.DisableUICraftingGrid)
        {
            var original = typeof(GuiDialogInventory).DeclaredMethod("ComposeSurvivalInvDialog");
            var prefix = typeof(GuiDialogInventoryPatch).DeclaredMethod("ComposeSurvivalInvDialogPrefix");
            var original2 = typeof(GuiDialogInventory).DeclaredMethod("OnGuiClosed");
            var prefix2 = typeof(GuiDialogInventoryPatch).DeclaredMethod("OnGuiClosedPrefix");

            harmony.Patch(original, prefix: prefix);
            harmony.Patch(original2, prefix: prefix2);
        }

    }

    private void TryLoadConfig()
    {
        string filename = Mod.Info.ModID + ".json";
        try
        {
            RknCraftingConfig config = api.LoadModConfig<RknCraftingConfig>(filename);
            if (config == null)
            {
                config = new RknCraftingConfig();
            }
            api.StoreModConfig<RknCraftingConfig>(config, filename);
            Config = config;
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Could not load config! Loading default settings instead.");
            Mod.Logger.Error(e);
            Config = new RknCraftingConfig();
        }
    }

    public void InitCatalog()
    {
        RecipeCatalog = new RecipeCatalog(api);
    }
}
