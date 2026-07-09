using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace RKN.Crafting.Patches;

// We only apply this patch if DisableUICraftingGrid is enabled
// Therefore do not use annotations that PatchAll will pick up

//[HarmonyPatch(typeof(GuiDialogInventory))]
public class GuiDialogInventoryPatch
{
    static MethodInfo onNewScrollbarValueMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "OnNewScrollbarvalue");
    static MethodInfo sendInvPacketMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "SendInvPacket");
    static MethodInfo closeIconPressedMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "CloseIconPressed");

    //[HarmonyPrefix]
    //[HarmonyPatch("ComposeSurvivalInvDialog")]
    static bool ComposeSurvivalInvDialogPrefix(GuiDialogInventory __instance, ref ICoreClientAPI ___capi, ref IInventory ___backpackInv, ref int ___prevRows, ref GuiComposer ___survivalInvDialog)
    {
        Action<float> OnNewScrollbarvalue = (Action<float>) Delegate.CreateDelegate(typeof(Action<float>), __instance, onNewScrollbarValueMethod);
        Action<object> SendInvPacket = (Action<object>) Delegate.CreateDelegate(typeof(Action<object>), __instance, sendInvPacketMethod);
        Action CloseIconPressed = (Action) Delegate.CreateDelegate(typeof(Action), __instance, closeIconPressedMethod);

        // ---- START CODE ----
        double elementToDialogPadding = GuiStyle.ElementToDialogPadding;
        double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
        int rows = (___prevRows = (int)Math.Ceiling((float)___backpackInv.Count / 6f));
        ElementBounds elementBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, unscaledSlotPadding, unscaledSlotPadding, 6, 7).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding);
        ElementBounds inventoryBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 0.0, 6, rows);
        ElementBounds insetBounds = elementBounds.ForkBoundingParent(3.0, 3.0, 3.0, 3.0);
        ElementBounds clipBounds = elementBounds.CopyOffsetedSibling();
        clipBounds.fixedHeight -= 3.0;
        //ElementBounds craftingGridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 0.0, 3, 3).FixedRightOf(insetBounds, 45.0);
        //craftingGridBounds.fixedY += 50.0;
        //ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 0.0, 1, 1).FixedRightOf(insetBounds, 45.0).FixedUnder(craftingGridBounds, 20.0);
        //outputSlotBounds.fixedX += unscaledSlotPadding + GuiElementPassiveItemSlot.unscaledSlotSize;
        //ElementBounds compoBounds = insetBounds.ForkBoundingParent(elementToDialogPadding, elementToDialogPadding + 30.0, elementToDialogPadding + craftingGridBounds.fixedWidth + 20.0, elementToDialogPadding);
        ElementBounds compoBounds = insetBounds.ForkBoundingParent(elementToDialogPadding, elementToDialogPadding + 30.0, elementToDialogPadding + 20.0, elementToDialogPadding);
        if (___capi.Settings.Bool["immersiveMouseMode"])
        {
            compoBounds.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-12.0, 0.0);
        }
        else
        {
            compoBounds.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(20.0, 0.0);
        }
        ElementBounds elementBounds8 = ElementStdBounds.VerticalScrollbar(insetBounds).WithParent(compoBounds);
        elementBounds8.fixedOffsetX -= 2.0;
        elementBounds8.fixedWidth = 15.0;
        ___survivalInvDialog = ___capi.Gui.CreateCompo("inventory-backpack", compoBounds).AddShadedDialogBG(ElementBounds.Fill).AddDialogTitleBar(Lang.Get("Inventory and Crafting"), CloseIconPressed)
            .AddVerticalScrollbar(OnNewScrollbarvalue, elementBounds8, "scrollbar")
            .AddInset(insetBounds, 3)
            .BeginClip(clipBounds)
            .AddItemSlotGridExcl(___backpackInv, SendInvPacket, 6, new int[4] { 0, 1, 2, 3 }, inventoryBounds, "slotgrid")
            .EndClip()
            //.AddItemSlotGrid(craftingInv, SendInvPacket, 3, new int[9] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, craftingGridBounds, "craftinggrid")
            //.AddItemSlotGrid(craftingInv, SendInvPacket, 1, new int[1] { 9 }, outputSlotBounds, "outputslot")
            .Compose();
        ___survivalInvDialog.GetScrollbar("scrollbar").SetHeights((float)elementBounds.fixedHeight, (float)(inventoryBounds.fixedHeight + unscaledSlotPadding));
        // ---- END CODE ----
        return false;
    }

    //[HarmonyPrefix]
    //[HarmonyPatch("OnGuiClosed")]
    static bool OnGuiClosedPrefix(GuiDialogInventory __instance, ref ICoreClientAPI ___capi, ref IInventory ___creativeInv, ref IInventory ___craftingInv, ref IInventory ___backpackInv, ref GuiComposer ___survivalInvDialog, ref GuiComposer ___creativeInvDialog)
    {
        if (___capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
        {
            ___creativeInvDialog?.GetTextInput("searchbox")?.SetValue("");
            ___creativeInvDialog?.GetSlotGrid("slotgrid")?.OnGuiClosed(___capi);
            ___capi.Network.SendPacketClient((Packet_Client)___creativeInv.Close(___capi.World.Player));
            //return;
            return false;
        }
        if (___craftingInv != null)
        {
            foreach (ItemSlot item in ___craftingInv)
            {
                if (!item.Empty)
                {
                    ItemStackMoveOperation op = new ItemStackMoveOperation(___capi.World, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, item.StackSize);
                    op.ActingPlayer = ___capi.World.Player;
                    object[] array = ___capi.World.Player.InventoryManager.TryTransferAway(item, ref op, onlyPlayerInventory: true);
                    int num = 0;
                    while (array != null && num < array.Length)
                    {
                        ___capi.Network.SendPacketClient((Packet_Client)array[num]);
                        num++;
                    }
                }
            }
            ___capi.World.Player.InventoryManager.DropAllInventoryItems(___craftingInv);
            ___capi.Network.SendPacketClient((Packet_Client)___craftingInv.Close(___capi.World.Player));
            //survivalInvDialog.GetSlotGrid("craftinggrid").OnGuiClosed(capi);
            //survivalInvDialog.GetSlotGrid("outputslot").OnGuiClosed(capi);
        }
        if (___survivalInvDialog != null)
        {
            ___capi.Network.SendPacketClient((Packet_Client)___backpackInv.Close(___capi.World.Player));
            ___survivalInvDialog.GetSlotGridExcl("slotgrid").OnGuiClosed(___capi);
        }

        return false;
    }
}