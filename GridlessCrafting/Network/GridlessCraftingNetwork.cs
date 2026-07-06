using RKN.GridlessCrafting.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RKN.GridlessCrafting.Network;

public class GridlessCraftingNetwork
{
    private ICoreAPI api;
    private INetworkChannel channel;

#pragma warning disable CS8603
    private IServerNetworkChannel ServerChannel { get { return channel as IServerNetworkChannel; } }
    private IClientNetworkChannel ClientChannel { get { return channel as IClientNetworkChannel; } }
    private ICoreClientAPI ClientApi { get { return api as ICoreClientAPI; } }
    private ICoreServerAPI ServerApi { get { return api as ICoreServerAPI; } }
#pragma warning restore CS8603

    public GridlessCraftingNetwork(ICoreClientAPI api, string modId)
    {
        this.api = api;
        channel = api.Network.RegisterChannel(modId);

        ClientChannel.RegisterMessageType<CreateCraftingBlockMessage>();
        ClientChannel.RegisterMessageType<CraftingStoppedMessage>();
        ClientChannel.RegisterMessageType<SelectNextRecipeMessage>();
        ClientChannel.SetMessageHandler<CraftingStoppedMessage>(OnCraftingStoppedMessage);
    }

    public GridlessCraftingNetwork(ICoreServerAPI api, string modId)
    {
        this.api = api;
        channel = api.Network.RegisterChannel(modId);

        ServerChannel.RegisterMessageType<CreateCraftingBlockMessage>();
        ServerChannel.RegisterMessageType<CraftingStoppedMessage>();
        ServerChannel.RegisterMessageType<SelectNextRecipeMessage>();
        ServerChannel.SetMessageHandler<CreateCraftingBlockMessage>(OnCreateCraftingBlockMessage);
        ServerChannel.SetMessageHandler<SelectNextRecipeMessage>(OnSelectNextRecipeMessage);
    }

    public void SelectNextRecipe(BlockPos pos)
    {
        ClientChannel.SendPacket(new SelectNextRecipeMessage() { Position = pos });
    }

    protected void OnSelectNextRecipeMessage(IServerPlayer fromPlayer, SelectNextRecipeMessage message)
    {
        api.World.BlockAccessor.GetBlockEntity<BlockEntityCraftingSurface>(message.Position).SelectNextRecipe();
    }

    public void SpawnCraftingSurface(BlockPos pos)
    {
        ClientChannel.SendPacket(new CreateCraftingBlockMessage() { Position = pos });
    }

    protected void OnCreateCraftingBlockMessage(IPlayer fromPlayer, CreateCraftingBlockMessage message)
    {
        BlockCraftingSurface.TryPlace(api, fromPlayer, message.Position, fromPlayer.InventoryManager.ActiveHotbarSlot);
    }

    public void StopCraftingAnimation(IPlayer craftingPlayer, EnumCraftingAnimation enumCraftingAnimation)
    {
        ServerChannel.SendPacket(new CraftingStoppedMessage() { animation = enumCraftingAnimation }, [(craftingPlayer as IServerPlayer)]);
    }

    protected void OnCraftingStoppedMessage(CraftingStoppedMessage message)
    {
        IPlayer player = ClientApi.World.Player;
        player.Entity.AnimManager.StopAnimation(PlayerAnimationRequest.ToAnimationCode(message.animation));
    }
}
