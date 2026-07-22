using RKN.Crafting.Animation;
using RKN.Crafting.Entities;
using RknCrafting;
using System;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RKN.Crafting.Network;

public class RknCraftingNetwork
{
    private ICoreAPI api;
    private INetworkChannel channel;
    private INetworkChannel channelUdp;

#pragma warning disable CS8603
    private IServerNetworkChannel ServerChannel { get { return channel as IServerNetworkChannel; } }
    private IClientNetworkChannel ClientChannel { get { return channel as IClientNetworkChannel; } }
    private IServerNetworkChannel ServerChannelUdp { get { return channelUdp as IServerNetworkChannel; } }
    private IClientNetworkChannel ClientChannelUdp { get { return channelUdp as IClientNetworkChannel; } }
    private ICoreClientAPI ClientApi { get { return api as ICoreClientAPI; } }
    private ICoreServerAPI ServerApi { get { return api as ICoreServerAPI; } }
#pragma warning restore CS8603

    public RknCraftingNetwork(ICoreClientAPI api, string modId)
    {
        this.api = api;
        channel = api.Network.RegisterChannel(modId);
        channelUdp = api.Network.RegisterUdpChannel(modId + "-udp");

        ClientChannel.RegisterMessageType<CreateCraftingBlockMessage>();
        ClientChannel.RegisterMessageType<CraftingStoppedMessage>();
        ClientChannel.RegisterMessageType<SelectNextRecipeMessage>();
        ClientChannel.RegisterMessageType<ConfigMessage>();
        ClientChannel.RegisterMessageType<ClientStartedCraftingMessage>();
        ClientChannel.SetMessageHandler<ConfigMessage>(OnConfigMessage);
        ClientChannel.SetMessageHandler<CraftingStoppedMessage>(OnCraftingStoppedMessage);
    }

    public RknCraftingNetwork(ICoreServerAPI api, string modId)
    {
        this.api = api;
        channel = api.Network.RegisterChannel(modId);
        channelUdp = api.Network.RegisterUdpChannel(modId + "-udp");

        ServerChannel.RegisterMessageType<CreateCraftingBlockMessage>();
        ServerChannel.RegisterMessageType<CraftingStoppedMessage>();
        ServerChannel.RegisterMessageType<SelectNextRecipeMessage>();
        ServerChannel.RegisterMessageType<ConfigMessage>();
        ServerChannel.RegisterMessageType<ClientStartedCraftingMessage>();
        ServerChannel.SetMessageHandler<CreateCraftingBlockMessage>(OnCreateCraftingBlockMessage);
        ServerChannel.SetMessageHandler<ClientStartedCraftingMessage>(OnClientStartedCraftingMessage);
    }

    public void SpawnCraftingSurface(BlockPos pos, bool asPlayer = true)
    {
        ClientChannel.SendPacket(new CreateCraftingBlockMessage() { Position = pos, asPlayer = asPlayer });
    }

    protected void OnCreateCraftingBlockMessage(IPlayer fromPlayer, CreateCraftingBlockMessage message)
    {
        BlockCraftingSurface.TryPlace(api, fromPlayer, message.Position, fromPlayer.InventoryManager.ActiveHotbarSlot);
    }

    public void StopCrafting(IPlayer player, EnumCraftingAnimation enumCraftingAnimation, BlockPos pos)
    {
        ServerChannel.SendPacket(new CraftingStoppedMessage() { Position = pos, animation = enumCraftingAnimation }, player as IServerPlayer);
    }

    protected void OnCraftingStoppedMessage(CraftingStoppedMessage message)
    {
        api.RcLogger().Debug("Received stop crafting message!");
        BlockEntityCraftingSurface entity = api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(message.Position);
        if (entity != null)
        {
            entity.ClientStopCrafting(message.animation);
        }
        else
        {
            ClientApi.RcPauseInteractions();
            api.RcAnimator().StopCrafting(ClientApi.World.Player, message.animation);
        }
    }

    public void TransferConfig(RknCraftingConfig config, IServerPlayer player)
    {
        api.RcLogger().Debug("Sending config to player {0}: {1}", [player.PlayerName, config]);
        ServerChannel.SendPacket(new ConfigMessage() { Config = config }, player);
    }

    private void OnConfigMessage(ConfigMessage message)
    {
        api.RcLogger().Debug("Received config from server: {0}", message.Config);
        api.RcSystem().ServerConfig = message.Config;
        api.RcSystem().InitCatalog();
    }

    public void ClientStartedCrafting(CraftingParams craftingParams, BlockPos pos)
    {
        ClientChannel.SendPacket(new ClientStartedCraftingMessage()
        {
            Position = pos,
            Animation = craftingParams.Animation,
            NextCraftingTime = craftingParams.NextCraftingTime,
            Bulk = craftingParams.Bulk,
            Recipe = craftingParams.Recipe.Id,
            RecipeCraftingTimeModifier = craftingParams.RecipeCraftingTimeModifier,
            Facing = craftingParams.Facing?.Flag ?? -1
        });
    }

    private void OnClientStartedCraftingMessage(IPlayer byPlayer, ClientStartedCraftingMessage message)
    {
        api.RcLogger().Debug("Received start crafting message from {0}!", byPlayer.PlayerName);
        api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(message.Position).ClientStartedCrafting(
            byPlayer, 
            message.Animation, 
            message.RecipeCraftingTimeModifier, 
            message.Recipe, 
            message.Bulk, 
            message.NextCraftingTime,
            message.Facing == -1 ? null : BlockFacing.FromFlag(message.Facing)
        );
    }
}
