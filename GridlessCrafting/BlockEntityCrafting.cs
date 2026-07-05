using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RKN.GridlessCrafting;

public class BlockEntityCrafting : BlockEntity
{
    private InventoryGeneric inventory;

    private int selectedRecipe = -1;
    private List<int>? validRecipes;
    private IPlayer? craftingPlayer;
    private EnumCraftingAnimation? craftingAnimation;
    private float timeoutTimer;
    private long tickListenerId;
    private float secondsLastCraft;

    public BlockEntityCrafting()
    {
        inventory = new InventoryGeneric(9, "craftingsurface", "0", null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        inventory.LateInitialize("crafting-" + Pos.ToString(), api);
        if (validRecipes != null)
        {
            // Don't persist selected recipe after server restart
            // TODO: will this desync on chunk reload?
            selectedRecipe = validRecipes[0];
        }
        if (Api.Side == EnumAppSide.Server)
        {
            tickListenerId = RegisterGameTickListener(OnTimeoutTick, 1000);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
        foreach (ItemSlot itemSlot in inventory)
        {
            if (itemSlot.Empty)
            {
                continue;
            }
            sb.Append(itemSlot.Itemstack.GetName());
            if (itemSlot.Itemstack.StackSize > 1)
            {
                sb.Append(" x");
                sb.Append(itemSlot.Itemstack.StackSize);
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        if (validRecipes != null)
        {
            foreach (int recipeId in validRecipes)
            {
                if (recipeId == selectedRecipe)
                {
                    sb.Append("-) ");
                }
                else
                {
                    sb.Append("   ");
                }
                sb.AppendLine("Recipe: " + RecipeCatalog.GetRecipeById(recipeId).Output.ResolvedItemStack.GetName());
            }
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        ITreeAttribute treeAttribute = tree.GetTreeAttribute("inventory");
        if (treeAttribute != null)
        {
            inventory.FromTreeAttributes(treeAttribute);
        }
        timeoutTimer = tree.GetFloat("timeoutTimer");
        selectedRecipe = tree.GetInt("selectedRecipe", -1);
        IAttribute validRecipesAttribute = tree["validRecipes"];
        if (validRecipesAttribute != null && validRecipesAttribute is IntArrayAttribute)
        {
            validRecipes = [.. (validRecipesAttribute as IntArrayAttribute).value];
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        TreeAttribute inventoryTree = new();
        inventory.ToTreeAttributes(inventoryTree);
        tree["inventory"] = inventoryTree;
        tree.SetFloat("timeoutTimer", timeoutTimer);
        tree.SetInt("selectedRecipe", selectedRecipe);
        if (validRecipes != null)
        {
            tree["validRecipes"] = new IntArrayAttribute(validRecipes.ToArray());
        }
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        base.OnBlockBroken(byPlayer);
        if (Api != null && Api.Side == EnumAppSide.Server)
        {
            inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 0);
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        ICoreClientAPI? capi = Api as ICoreClientAPI;
        for (int i = 0; i < inventory.Count; i++)
        {
            ItemSlot slot = inventory[i];
            if (slot?.Itemstack == null || slot.Itemstack.StackSize == 0)
            {
                continue;
            }
            ItemStack itemstack = slot.Itemstack;
            MeshData? meshData = null;
            if (itemstack.Class == EnumItemClass.Block)
            {
                meshData = capi.TesselatorManager.GetDefaultBlockMesh(itemstack.Block).Clone();
            }
            else
            {
                IContainedMeshSource containedMeshSource = itemstack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
                if (containedMeshSource != null)
                {
                    meshData = containedMeshSource.GenMesh(slot, capi.BlockTextureAtlas, Pos);
                }
            }
            if (meshData == null)
            {
                continue;
            }
            meshData.Scale(0.25f, 0.25f, 0.25f);
            MeshSurfaceTranslate(meshData, i);
            BottomCenterMesh(meshData);
            mesher.AddMeshData(meshData);
        }
        return true;
    }

    private void MeshSurfaceTranslate(MeshData meshData, int slot)
    {
        if (meshData.VerticesCount == 0)
        {
            return;
        }
        /*Random random = new();
        meshData.Translate((random.NextSingle() - 0.5f) % 0.5f, 0, (random.NextSingle() - 0.5f) % 0.5f);*/
        if (slot == 1)
        {
            meshData.Translate(0.3f, 0, 0.3f);
            meshData.Scale(0.95f, 0.95f, 0.95f);
        }
        else if (slot == 2)
        {
            meshData.Translate(-0.3f, 0, 0.3f);
            meshData.Scale(1.05f, 1.05f, 1.05f);
        }
        else if (slot == 3)
        {
            meshData.Translate(-0.3f, 0, -0.3f);
            meshData.Scale(1.02f, 1.02f, 1.02f);
        }
        else if (slot == 4)
        {
            meshData.Translate(0.3f, 0, -0.3f);
            meshData.Scale(0.98f, 0.98f, 0.98f);
        }
        else if (slot == 5)
        {
            meshData.Translate(0.3f, 0, 0);
            meshData.Scale(0.90f, 0.90f, 0.90f);
        }
        else if (slot == 6)
        {
            meshData.Translate(0, 0, 0.3f);
            meshData.Scale(1.02f, 1.02f, 1.02f);
        }
        else if (slot == 7)
        {
            meshData.Translate(-0.3f, 0, 0);
            meshData.Scale(0.93f, 0.93f, 0.93f);
        }
        else if (slot == 8)
        {
            meshData.Translate(0, 0, -0.3f);
            meshData.Scale(1.04f, 1.04f, 1.04f);
        }
    }

    private float[] NormalizeSize(float[] size)
    {
        float num = size[0];
        if (size[1] > num)
        {
            num = size[1];
        }
        if (size[2] > num)
        {
            num = size[2];
        }
        if (num <= 0.0001f)
        {
            return new float[3] { 1f, 1f, 1f };
        }
        return new float[3]
        {
            size[0] / num,
            size[1] / num,
            size[2] / num
        };
    }

    private void BottomCenterMesh(MeshData mesh)
    {
        if (mesh.VerticesCount <= 0)
        {
            return;
        }
        GetMeshBounds(mesh, out Vec3f min, out Vec3f max);
        mesh.Translate(new Vec3f(0, -min.Y, 0));
    }

    private static void GetMeshBounds(MeshData mesh, out Vec3f min, out Vec3f max)
    {
        min = new Vec3f(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vec3f(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < mesh.VerticesCount; i++)
        {
            int num = i * 3;
            float num2 = mesh.xyz[num];
            float num3 = mesh.xyz[num + 1];
            float num4 = mesh.xyz[num + 2];
            if (num2 < min.X)
            {
                min.X = num2;
            }
            if (num3 < min.Y)
            {
                min.Y = num3;
            }
            if (num4 < min.Z)
            {
                min.Z = num4;
            }
            if (num2 > max.X)
            {
                max.X = num2;
            }
            if (num3 > max.Y)
            {
                max.Y = num3;
            }
            if (num4 > max.Z)
            {
                max.Z = num4;
            }
        }
    }

    public bool IsCrafting(IPlayer byPlayer)
    {
        return craftingPlayer == byPlayer;
    }

    public PlayerAnimationRequest? StartCrafting(IWorldAccessor world, IPlayer byPlayer, BlockCrafting blockCrafting)
    {
        timeoutTimer = 0;
        if (craftingPlayer != null || selectedRecipe == -1)
        {
            return null;
        }
        (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(byPlayer);
        if (items == null || !RecipeCatalog.MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
        {
            return null;
        }
        craftingPlayer = byPlayer;
        craftingAnimation = GetCraftingAnimation(selectedRecipe, primaryTool, offhandTool);
        Api.Logger.Debug("[gridlesscrafting] Crafting {0} by {1}!", [RecipeCatalog.GetRecipeById(selectedRecipe).Name, craftingPlayer.PlayerName]);
        if (world.Api.Side == EnumAppSide.Server)
        {
            MarkDirty();
        }
        return new PlayerAnimationRequest((EnumCraftingAnimation)craftingAnimation, EnumAnimationAction.START);;
    }

    public PlayerAnimationRequest? OnCraftingStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.Side != EnumAppSide.Server)
        {
            return null;
        }
        timeoutTimer = 0;
        if (secondsUsed > (secondsLastCraft + 1) && IsCrafting(byPlayer))
        {
            CreateOutput(world);
            
            // Continue crafting if possible
            (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(craftingPlayer);
            if (items == null || items.Count == 0)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
            } else
            {
                validRecipes = RecipeCatalog.GetValidRecipesWithoutTools(items);
            }
            if (items == null || !RecipeCatalog.MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
            {
                EnumCraftingAnimation enumCraftingAnimation = GetCraftingAnimation();
                ResetState();
                selectedRecipe = -1;
                (Api as ICoreServerAPI).Network.GetChannel("rkngridlesscrafting").SendPacket(new CraftingStoppedMessage() {animation = enumCraftingAnimation}, [(byPlayer as IServerPlayer)]);
                return new PlayerAnimationRequest(enumCraftingAnimation, EnumAnimationAction.STOP);
            }
            MarkDirty(true, null);
            secondsLastCraft = secondsUsed;
        }
        return null;
    }

    public PlayerAnimationRequest? CancelCrafting(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        timeoutTimer = 0;
        if (craftingPlayer?.ClientId != byPlayer.ClientId)
        {
            return null;
        }
        Api.Logger.Debug("[gridlesscrafting] Cancelled crafting by {0}!", [craftingPlayer.PlayerName]);
        EnumCraftingAnimation anim = GetCraftingAnimation();
        ResetState();
        return new PlayerAnimationRequest(anim, EnumAnimationAction.STOP);
    }

    private EnumCraftingAnimation GetCraftingAnimation()
    {
        return (EnumCraftingAnimation)(craftingAnimation == null ? EnumCraftingAnimation.HandsMixing : craftingAnimation);
    }

    public bool TryPutIngredient(ItemSlot slot, IPlayer byPlayer)
    {
        timeoutTimer = 0;
        if (Api.Side != EnumAppSide.Server)
        {
            return false;
        }
        if (slot.Itemstack?.Item?.Tool != null)
        {
            return false;
        }
        foreach (ItemSlot invSlot in inventory)
        {
            if (invSlot.CanTakeFrom(slot))
            {
                int quantity = 1;
                if (byPlayer.Entity.Controls.CtrlKey)
                {
                    quantity = slot.StackSize;
                }
                if (slot.TryPutInto(Api.World, invSlot, quantity) < 1)
                {
                    return false;
                }
                slot.MarkDirty();

                (List<ItemSlot>? items, ItemSlot? _, ItemSlot? _) = GetCraftingItems(byPlayer);
                List<int> recipes = RecipeCatalog.GetValidRecipesWithoutTools(items);
                validRecipes = recipes;
                selectedRecipe = -1;
                if (recipes.Count > 0)
                {
                    selectedRecipe = validRecipes[0];
                }

                MarkDirty(true, null);
                return true;
            }
        }
        return false;
    }

    public void SelectNextRecipe()
    {
        if (validRecipes == null || craftingPlayer != null)
        {
            return;
        }
        for (int i = 0; i < validRecipes.Count; i++)
        {
            if (validRecipes[i] == selectedRecipe)
            {
                selectedRecipe = i == validRecipes.Count-1 ? validRecipes[0] : validRecipes[i+1];
                MarkDirty(true, null);
                return;
            }
        }
    }

    public void OnTimeoutTick(float dt)
    {
        timeoutTimer += dt;
        if(timeoutTimer >= 120)
        {
            Api.World.BlockAccessor.BreakBlock(Pos, null);
        }
    }

    private void ConsumeRecipe(GridRecipe recipe, List<ItemSlot> items, ItemSlot? primaryTool, ItemSlot? offhandTool, IWorldAccessor world)
    {
        foreach (CraftingRecipeIngredient? ingredient in recipe.ResolvedIngredients)
        {
            if (ingredient == null)
            {
                continue;
            }
            if (!ingredient.Consume)
            {
                if (primaryTool != null && ingredient.ToolDurabilityCost > 0 && ingredient.SatisfiesAsIngredient(primaryTool.Itemstack, true))
                {
                    primaryTool.Itemstack.Collectible.DamageItem(world, craftingPlayer.Entity, primaryTool, ingredient.ToolDurabilityCost, ingredient.Break);
                    primaryTool.MarkDirty();
                    continue;
                }
                else if (offhandTool != null && ingredient.ToolDurabilityCost > 0 && ingredient.SatisfiesAsIngredient(offhandTool.Itemstack, true))
                {
                    offhandTool.Itemstack.Collectible.DamageItem(world, craftingPlayer.Entity, offhandTool, ingredient.ToolDurabilityCost, ingredient.Break);
                    offhandTool.MarkDirty();
                    continue;
                }
            }
            else
            {
                foreach (ItemSlot stack in items)
                {
                    if (stack.StackSize > 0 && ingredient.SatisfiesAsIngredient(stack.Itemstack, true))
                    {
                        stack.TakeOut(ingredient.Quantity);
                        stack.MarkDirty();
                        goto CONTINUE;
                    }
                }
                return;
            }
            CONTINUE:;
        }
    }

    private (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) GetCraftingItems(IPlayer byPlayer)
    {
        List<ItemSlot> items = inventory.Where(s => s != null && s.StackSize > 0).ToList();
        if (items.Count == 0)
        {
            return (null, null, null);
        }
        IPlayerInventoryManager inventoryManager = byPlayer.InventoryManager;
        ItemSlot? primaryTool = inventoryManager.ActiveTool != null ? inventoryManager.ActiveHotbarSlot : null;
        ItemSlot? offhandTool = inventoryManager.OffhandTool != null ? inventoryManager.OffhandHotbarSlot : null;
        return (items, primaryTool, offhandTool);
    }

    private void CreateOutput(IWorldAccessor world)
    {
        if (craftingPlayer == null || selectedRecipe == null)
        {
            return;
        }
        (List<ItemSlot>? items, ItemSlot? primaryTool, ItemSlot? offhandTool) = GetCraftingItems(craftingPlayer);
        if (items == null || !RecipeCatalog.MatchesRecipe(items, primaryTool, offhandTool, selectedRecipe))
        {
            return;
        }
        GridRecipe gridRecipe = RecipeCatalog.GetRecipeById(selectedRecipe);
        Api.Logger.Debug("[gridlesscrafting] Crafted {0} by {1}!", [gridRecipe.Name, craftingPlayer.PlayerName]);
        ItemStack result = gridRecipe.Output.ResolvedItemStack.Clone();
        if (!result.ResolveBlockOrItem(world))
        {
            return;
        }
        //result.Collectible.OnCreatedByCrafting(Array.Empty<ItemSlot>(), new DummySlot(result), gridRecipe);
        Api.World.SpawnItemEntity(gridRecipe.Output.ResolvedItemStack.Clone(), Pos);
        ConsumeRecipe(gridRecipe, items, primaryTool, offhandTool, world);
    }

    private static EnumCraftingAnimation GetCraftingAnimation(int recipe, ItemSlot? primaryTool, ItemSlot? offhandTool)
    {
        if (primaryTool == null)
        {
            if (RecipeCatalog.GetRecipeById(recipe).Output?.ResolvedItemStack?.Item?.Tool != null) {
                return EnumCraftingAnimation.HandsTool;
            }
            return EnumCraftingAnimation.HandsMixing;
        }
        EnumTool? primary = primaryTool?.Itemstack?.Item?.Tool;
        EnumTool? offhand = offhandTool?.Itemstack?.Item?.Tool;
        return primary switch
        {
            EnumTool.Knife => EnumCraftingAnimation.Knife,
            EnumTool.Axe => offhand == EnumTool.Hammer ? EnumCraftingAnimation.AxeHammer : EnumCraftingAnimation.Axe,
            EnumTool.Hammer => EnumCraftingAnimation.Hammer,
            EnumTool.Shears => EnumCraftingAnimation.Shears,
            EnumTool.Saw => EnumCraftingAnimation.Saw,
            EnumTool.Chisel => offhand == EnumTool.Hammer ? EnumCraftingAnimation.ChiselHammer : EnumCraftingAnimation.Chisel,
            EnumTool.Club => EnumCraftingAnimation.Club,
            _ => EnumCraftingAnimation.HandsMixing
        };

    }

    private void ResetState()
    {
        craftingPlayer = null;
        craftingAnimation = null;
        secondsLastCraft = 0;
        MarkDirty();
    }
}