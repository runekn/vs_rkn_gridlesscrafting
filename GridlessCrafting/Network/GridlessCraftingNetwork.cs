using RKN.GridlessCrafting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RKN.GridlessCrafting.Network;

public class GridlessCraftingNetwork
{
    private static ICoreAPI api;
    private static string modId;
    private static ICoreClientAPI ClientApi { get { return api as ICoreClientAPI; } }
    private static ICoreServerAPI ServerApi { get { return api as ICoreServerAPI; } }

    public static void Initialize(ICoreClientAPI api, string modId)
    {
        GridlessCraftingNetwork.api = api;
        GridlessCraftingNetwork.modId = modId;
        IClientNetworkChannel channel = api.Network.RegisterChannel(modId);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        channel.RegisterMessageType<CraftingStoppedMessage>();
        channel.RegisterMessageType<SelectNextRecipeMessage>();
        channel.SetMessageHandler<CraftingStoppedMessage>(OnCraftingStoppedMessage);
    }

    public static void Shutdown()
    {
        api = null;
        modId = null;
    }

    public static void Initialize(ICoreServerAPI api, string modId)
    {
        GridlessCraftingNetwork.api = api;
        GridlessCraftingNetwork.modId = modId;
        IServerNetworkChannel channel = api.Network.RegisterChannel(modId);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        channel.RegisterMessageType<CraftingStoppedMessage>();
        channel.RegisterMessageType<SelectNextRecipeMessage>();
        channel.SetMessageHandler<CreateCraftingBlockMessage>(OnCreateCraftingBlockMessage);
        channel.SetMessageHandler<SelectNextRecipeMessage>(OnSelectNextRecipeMessage);
    }

    public static void SelectNextRecipe(BlockPos pos)
    {
        ClientApi.Network.GetChannel(modId).SendPacket(new SelectNextRecipeMessage() { Position = pos });
    }

    protected static void OnSelectNextRecipeMessage(IServerPlayer fromPlayer, SelectNextRecipeMessage message)
    {
        api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(message.Position).SelectNextRecipe();
    }

    public static void SpawnCraftingSurface(BlockPos pos)
    {
        ClientApi.Network.GetChannel(modId).SendPacket(new CreateCraftingBlockMessage() { Position = pos });
    }

    protected static void OnCreateCraftingBlockMessage(IPlayer fromPlayer, CreateCraftingBlockMessage message)
    {
        (api.World.GetBlock(new AssetLocation("rkngridlesscrafting:craftingsurface")) as BlockCrafting).TryPlace(fromPlayer, message.Position, fromPlayer.InventoryManager.ActiveHotbarSlot);
    }

    public static void StopCraftingAnimation(IPlayer craftingPlayer, EnumCraftingAnimation enumCraftingAnimation)
    {
        ServerApi.Network.GetChannel(modId).SendPacket(new CraftingStoppedMessage() { animation = enumCraftingAnimation }, [(craftingPlayer as IServerPlayer)]);
    }

    protected static void OnCraftingStoppedMessage(CraftingStoppedMessage message)
    {
        IPlayer player = ClientApi.World.Player;
        player.Entity.AnimManager.StopAnimation(PlayerAnimationRequest.ToAnimationCode(message.animation));
    }
}
