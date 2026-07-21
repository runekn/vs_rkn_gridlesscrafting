using System;
using RKN.Crafting;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RknCrafting;

internal class RecipeSelectionDialog : GuiDialog
{
    private IInventory recipeInventory;
    private BlockPos pos;
    public override string ToggleKeyCombinationCode => null;
    
    private readonly double floatyDialogPosition = 0.5;
    private readonly double floatyDialogAlign = 0.75;

    public RecipeSelectionDialog(ICoreClientAPI capi, BlockPos pos) : base(capi)
    {
        this.pos = pos;
    }
    
    public bool TryOpen(ScanResult[] recipes, Action<int> selected)
    {
        recipeInventory = new RecipeSelectionInventory(capi, recipes, i => selected(recipes[i].Wrapper.Id));
        return TryOpen();
    }

    public override void OnGuiOpened()
    {
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        ClearComposers();
        double elementToDialogPadding = GuiStyle.ElementToDialogPadding;
        double unscaledSlotPadding = GuiElementItemSlotGridBase.unscaledSlotPadding;
        int rows = (int)Math.Ceiling(recipeInventory.Count / 6f);
        ElementBounds elementBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, unscaledSlotPadding, unscaledSlotPadding, 6, 2).FixedGrow(2.0 * unscaledSlotPadding, 2.0 * unscaledSlotPadding);
        ElementBounds inventoryBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 0.0, 6, rows);
        ElementBounds insetBounds = elementBounds.ForkBoundingParent(3.0, 3.0, 3.0, 3.0);
        ElementBounds clipBounds = elementBounds.CopyOffsetedSibling();
        clipBounds.fixedHeight -= 3.0;
        ElementBounds compoBounds = insetBounds.ForkBoundingParent(elementToDialogPadding, elementToDialogPadding + 30.0, elementToDialogPadding + 20.0, elementToDialogPadding);
        compoBounds.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(20.0, 0.0);
        ElementBounds scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds).WithParent(compoBounds);
        scrollbarBounds.fixedOffsetX -= 2.0;
        scrollbarBounds.fixedWidth = 15.0;
        SingleComposer = capi.Gui.CreateCompo("inventory-recipes", compoBounds).AddShadedDialogBG(ElementBounds.Fill).AddDialogTitleBar("Select Recipe", CloseIconPressed)
            .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
            .AddInset(insetBounds, 3)
            .BeginClip(clipBounds)
            .AddItemSlotGrid(recipeInventory, null, 6, inventoryBounds, "slotgrid")
            .EndClip()
            .Compose();
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)elementBounds.fixedHeight, (float)(inventoryBounds.fixedHeight + unscaledSlotPadding));
    }
    
    private void CloseIconPressed() => TryClose();

    private void OnNewScrollbarvalue(float value)
    {
        if (!IsOpened())
            return;
        ElementBounds bounds = SingleComposer.GetSlotGrid("slotgrid").Bounds;
        bounds.fixedY = 10.0 - GuiElementItemSlotGridBase.unscaledSlotPadding - value;
        bounds.CalcWorldBounds();
    }
    
    /*public override void OnRenderGUI(float deltaTime)
    {
        if (capi.Settings.Bool["immersiveMouseMode"])
        {
            Vec3d vec3d = MatrixToolsd.Project(new Vec3d(pos.X + 0.5, pos.Y + floatyDialogPosition, pos.Z + 0.5), capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);
            if (vec3d.Z < 0.0)
                return;
            SingleComposer.Bounds.Alignment = EnumDialogArea.None;
            SingleComposer.Bounds.fixedOffsetX = 0.0;
            SingleComposer.Bounds.fixedOffsetY = 0.0;
            SingleComposer.Bounds.absFixedX = vec3d.X - SingleComposer.Bounds.OuterWidth / 2.0;
            SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - vec3d.Y - SingleComposer.Bounds.OuterHeight * floatyDialogAlign;
            SingleComposer.Bounds.absMarginX = 0.0;
            SingleComposer.Bounds.absMarginY = 0.0;
        }
        base.OnRenderGUI(deltaTime);
    }*/
}

public class RecipeSelectionInventory : InventoryBase
{
    private ItemSlot[] slots;
    private Action<int> selected;

    public RecipeSelectionInventory(ICoreAPI api, ScanResult[] recipes, Action<int> selected) : base("recipeSelection", "0", api)
    {
        slots = GenEmptySlots(recipes.Length);
        this.selected = selected;
        for (var index = 0; index < recipes.Length; index++)
        {
            var wrapper = recipes[index];
            ItemStack itemStack = wrapper.Output;
            slots[index].Itemstack = itemStack;
        }
    }

    public override ItemSlot this[int slotId] { get => slots[slotId]; set => throw new NotImplementedException(); }

    public override int Count => slots.Length;

    public override object? ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
    {
        selected(slotId);
        return null;
    }

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        throw new NotImplementedException();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        throw new NotImplementedException();
    }
}
