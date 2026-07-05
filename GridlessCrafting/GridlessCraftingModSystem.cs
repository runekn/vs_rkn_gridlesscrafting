using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RKN.GridlessCrafting;

public class GridlessCraftingModSystem : ModSystem
{
    private ICoreAPI api;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        this.api = api;
        api.RegisterBlockClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockCrafting));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockEntityCraftingSurface));
        api.RegisterBlockBehaviorClass(Mod.Info.ModID + ".spawncraftingsurface", typeof(BlockBehaviorSpawnCraftingSurface));
        var harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();
        api.Logger.Debug("[gridlesscrafting] Hello world!");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.LevelFinalize += InitCatalog;
        IClientNetworkChannel channel = api.Network.RegisterChannel(Mod.Info.ModID);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        channel.RegisterMessageType<CraftingStoppedMessage>();
        channel.RegisterMessageType<SelectNextRecipeMessage>();
        channel.SetMessageHandler<CraftingStoppedMessage>(OnCraftingStoppedMessage);
        api.Input.RegisterHotKey("rkngridlesscrafting.start", Lang.Get("hotkey-crafting"), GlKeys.AltLeft);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        IServerNetworkChannel channel = api.Network.RegisterChannel(Mod.Info.ModID);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        channel.RegisterMessageType<CraftingStoppedMessage>();
        channel.RegisterMessageType<SelectNextRecipeMessage>();
        channel.SetMessageHandler<CreateCraftingBlockMessage>(OnCreateCraftingBlockMessage);
        channel.SetMessageHandler<SelectNextRecipeMessage>(OnSelectNextRecipeMessage);
        InitCatalog();
    }

    public void OnSelectNextRecipeMessage(IServerPlayer fromPlayer, SelectNextRecipeMessage message)
    {
        api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(message.Position).SelectNextRecipe();
    }

    public void OnCreateCraftingBlockMessage(IPlayer fromPlayer, CreateCraftingBlockMessage message)
    {
        api.World.BlockAccessor.GetBlock(message.Position).GetBehavior<BlockBehaviorSpawnCraftingSurface>().TryPlaceCrafting(api.World, fromPlayer, message.Position);
    }

    public void OnCraftingStoppedMessage(CraftingStoppedMessage message)
    {
        IPlayer player = (api as ICoreClientAPI).World.Player;
        player.Entity.AnimManager.StopAnimation(PlayerAnimationRequest.ToAnimationCode(message.animation));
    }

    public void InitCatalog()
    {
        RecipeCatalog.Initialize(api);
    }
}
