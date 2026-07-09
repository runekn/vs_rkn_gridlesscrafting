using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace RKN.Crafting.Patches;

#pragma warning disable CS8600

[HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
public class GuiComposerPatchComposeSurvivalInvDialog
{
    static FieldInfo capiField = AccessTools.Field(typeof(GuiDialogInventory), "capi");
    static FieldInfo backpackInvField = AccessTools.Field(typeof(GuiDialogInventory), "backpackInv");
    static FieldInfo prevRowsField = AccessTools.Field(typeof(GuiDialogInventory), "prevRows");
    static MethodInfo onNewScrollbarValueMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "OnNewScrollbarvalue");
    static MethodInfo sendInvPacketMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "SendInvPacket");
    static MethodInfo closeIconPressedMethod = AccessTools.DeclaredMethod(typeof(GuiDialogInventory), "CloseIconPressed");

    static bool Prefix(GuiDialogInventory __instance)
    {
        ICoreClientAPI capi = capiField.GetValue(__instance) as ICoreClientAPI;
        IInventory backpackInv = backpackInvField.GetValue(__instance) as IInventory;

        Action<float> OnNewScrollbarvalue = (Action<float>) Delegate.CreateDelegate(typeof(Action<float>), __instance, onNewScrollbarValueMethod);
        Action<object> SendInvPacket = (Action<object>) Delegate.CreateDelegate(typeof(Action<object>), __instance, sendInvPacketMethod);
        Action CloseIconPressed = (Action) Delegate.CreateDelegate(typeof(Action), __instance, closeIconPressedMethod);

        int prevRows = (int)Math.Ceiling((float)backpackInv.Count / 6f);
        prevRowsField.SetValue(__instance, prevRows);
        int rows = prevRows;

        GuiComposer survivalInvDialog;


        // ---- START CODE ----
        double elementToDialogPadding = GuiStyle.ElementToDialogPadding;
        double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
        //int rows = (prevRows = (int)Math.Ceiling((float)backpackInv.Count / 6f));
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
        if (capi.Settings.Bool["immersiveMouseMode"])
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
        survivalInvDialog = capi.Gui.CreateCompo("inventory-backpack", compoBounds).AddShadedDialogBG(ElementBounds.Fill).AddDialogTitleBar(Lang.Get("Inventory and Crafting"), CloseIconPressed)
            .AddVerticalScrollbar(OnNewScrollbarvalue, elementBounds8, "scrollbar")
            .AddInset(insetBounds, 3)
            .BeginClip(clipBounds)
            .AddItemSlotGridExcl(backpackInv, SendInvPacket, 6, new int[4] { 0, 1, 2, 3 }, inventoryBounds, "slotgrid")
            .EndClip()
            //.AddItemSlotGrid(craftingInv, SendInvPacket, 3, new int[9] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, craftingGridBounds, "craftinggrid")
            //.AddItemSlotGrid(craftingInv, SendInvPacket, 1, new int[1] { 9 }, outputSlotBounds, "outputslot")
            .Compose();
        survivalInvDialog.GetScrollbar("scrollbar").SetHeights((float)elementBounds.fixedHeight, (float)(inventoryBounds.fixedHeight + unscaledSlotPadding));
        // ---- END CODE ----


        AccessTools.Field(typeof(GuiDialogInventory), "survivalInvDialog").SetValue(__instance, survivalInvDialog);
        return false;
    }
}

[HarmonyPatch(typeof(GuiDialogInventory), "OnGuiClosed")]
public class GuiComposerPatchOnGuiClosed
{
    static FieldInfo capiField = AccessTools.Field(typeof(GuiDialogInventory), "capi");
    static FieldInfo creativeInvField = AccessTools.Field(typeof(GuiDialogInventory), "creativeInv");
    static FieldInfo craftingInvField = AccessTools.Field(typeof(GuiDialogInventory), "craftingInv");
    static FieldInfo backpackInvField = AccessTools.Field(typeof(GuiDialogInventory), "backpackInv");
    static FieldInfo survivalInvDialogField = AccessTools.Field(typeof(GuiDialogInventory), "survivalInvDialog");
    static FieldInfo creativeInvDialogField = AccessTools.Field(typeof(GuiDialogInventory), "creativeInvDialog");

    static bool Prefix(GuiDialogInventory __instance)
    {
        ICoreClientAPI capi = capiField.GetValue(__instance) as ICoreClientAPI;
        IInventory creativeInv = creativeInvField.GetValue(__instance) as IInventory;
        IInventory craftingInv = craftingInvField.GetValue(__instance) as IInventory;
        IInventory backpackInv = backpackInvField.GetValue(__instance) as IInventory;
        GuiComposer survivalInvDialog = survivalInvDialogField.GetValue(__instance) as GuiComposer;
        GuiComposer creativeInvDialog = creativeInvDialogField.GetValue(__instance) as GuiComposer;

        // ---- START CODE ----
        if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
        {
            creativeInvDialog?.GetTextInput("searchbox")?.SetValue("");
            creativeInvDialog?.GetSlotGrid("slotgrid")?.OnGuiClosed(capi);
            capi.Network.SendPacketClient((Packet_Client)creativeInv.Close(capi.World.Player));
            //return;
            return false;
        }
        if (craftingInv != null)
        {
            foreach (ItemSlot item in craftingInv)
            {
                if (!item.Empty)
                {
                    ItemStackMoveOperation op = new ItemStackMoveOperation(capi.World, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, item.StackSize);
                    op.ActingPlayer = capi.World.Player;
                    object[] array = capi.World.Player.InventoryManager.TryTransferAway(item, ref op, onlyPlayerInventory: true);
                    int num = 0;
                    while (array != null && num < array.Length)
                    {
                        capi.Network.SendPacketClient((Packet_Client)array[num]);
                        num++;
                    }
                }
            }
            capi.World.Player.InventoryManager.DropAllInventoryItems(craftingInv);
            capi.Network.SendPacketClient((Packet_Client)craftingInv.Close(capi.World.Player));
            //survivalInvDialog.GetSlotGrid("craftinggrid").OnGuiClosed(capi);
            //survivalInvDialog.GetSlotGrid("outputslot").OnGuiClosed(capi);
        }
        if (survivalInvDialog != null)
        {
            capi.Network.SendPacketClient((Packet_Client)backpackInv.Close(capi.World.Player));
            survivalInvDialog.GetSlotGridExcl("slotgrid").OnGuiClosed(capi);
        }

        return false;
    }
}
#pragma warning restore CS8600