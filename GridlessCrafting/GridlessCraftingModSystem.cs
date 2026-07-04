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
        api.RegisterBlockClass(Mod.Info.ModID + ".craftingblock", typeof(BlockCrafting));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".craftingblock", typeof(BlockEntityCrafting));
        api.RegisterBlockBehaviorClass(Mod.Info.ModID + ".craftingsurface", typeof(BlockBehaviorCraftingSurface));
        api.RegisterCollectibleBehaviorClass(Mod.Info.ModID + ".craftingstart", typeof(BehaviorStartCrafting));
        api.RegisterItemClass(Mod.Info.ModID + ".craftingwand", typeof(ItemCraftingWand));
        api.Logger.Debug("[gridlesscrafting] Hello world!");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.LevelFinalize += InitCatalog;
        IClientNetworkChannel channel = api.Network.RegisterChannel(Mod.Info.ModID);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        api.Input.RegisterHotKey("rkngridlesscrafting.start", Lang.Get("hotkey-crafting"), GlKeys.AltLeft);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        IServerNetworkChannel channel = api.Network.RegisterChannel(Mod.Info.ModID);
        channel.RegisterMessageType<CreateCraftingBlockMessage>();
        channel.SetMessageHandler<CreateCraftingBlockMessage>(OnCreateCraftingBlockMessage);
        InitCatalog();
    }

    public void OnCreateCraftingBlockMessage(IPlayer fromPlayer, CreateCraftingBlockMessage message)
    {
        api.World.BlockAccessor.GetBlock(message.Position).GetBehavior<BlockBehaviorCraftingSurface>().TryPlaceCrafting(api.World, fromPlayer, message.Position);
    }

    public void InitCatalog()
    {
        RecipeCatalog.Initialize(api);
    }
}
