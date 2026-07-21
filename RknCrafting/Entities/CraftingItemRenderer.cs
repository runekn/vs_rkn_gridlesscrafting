using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RknCrafting.Entities;

internal class CraftingItemRenderer
{
    private static readonly SurfacePosTransform R1_C1 = new(0.2f, 0.2f, 0.95f);
    private static readonly SurfacePosTransform R2_C1 = new(0.2f, 0.5f, 1.01f);
    private static readonly SurfacePosTransform R3_C1 = new(0.2f, 0.8f, 1.02f);
    private static readonly SurfacePosTransform R1_C2 = new(0.5f, 0.2f, 1.02f);
    private static readonly SurfacePosTransform R2_C2 = new(0.5f, 0.5f, 1);
    private static readonly SurfacePosTransform R3_C2 = new(0.5f, 0.9f, 0.98f);
    private static readonly SurfacePosTransform R1_C3 = new(0.8f, 0.2f, 1.02f);
    private static readonly SurfacePosTransform R2_C3 = new(0.9f, 0.5f, 0.97f);
    private static readonly SurfacePosTransform R3_C3 = new(0.8f, 0.8f, 1.02f);

    public static float[][] GenTransformationMatrices(InventoryGeneric inventory, string transformCode, bool gridless, Block block, System.Func<ItemSlot, MeshData> getMesh)
    {
        float[][] tfMatrices = new float[inventory.Count][];

        for (int index = 0; index < inventory.Count; index++)
        {
            ItemSlot itemSlot = inventory[index];
            ModelTransform? customTransform = null;
            if (itemSlot.Empty || itemSlot.Itemstack.StackSize <= 0)
            {
                continue;
            }

            FastVec3f scale = Vec3f.One;

            customTransform = itemSlot.Itemstack.Collectible?.Attributes?[transformCode].AsObject<ModelTransform>();
            if (customTransform == null)
            {
                scale = scale.Mul(0.30f);
                MeshData meshData = getMesh(itemSlot);
                if (meshData != null)
                {
                    float itemSize = GetMeshXZSize(meshData);
                    scale = scale.Set(scale.X / itemSize, scale.Y / itemSize, scale.Z / itemSize);
                }
            }

            // Get grid slot translations, and a tiny bit of scale variance.
            SurfacePosTransform posTransform;
            if (gridless)
            {
                posTransform = index switch
                {
                    0 => R2_C2,
                    1 => R1_C1,
                    2 => R1_C3,
                    3 => R3_C2,
                    4 => R3_C3,
                    5 => R2_C1,
                    6 => R1_C2,
                    7 => R2_C3,
                    8 => R3_C1,
                };
            }
            else
            {
                posTransform = index switch
                {
                    0 => R1_C1,
                    1 => R1_C2,
                    2 => R1_C3,
                    3 => R2_C1,
                    4 => R2_C2,
                    5 => R2_C3,
                    6 => R3_C1,
                    7 => R3_C2,
                    8 => R3_C3,
                };
            }

            scale = scale.Mul(posTransform.scale);

            Matrixf matrixf = new Matrixf()
                .Scale(scale.X, scale.Y, scale.Z)                       // First scale
                .Translate(-0.5f, 0, -0.5f)                             // Then center it
                .Translate(posTransform.X / scale.X, 0, posTransform.Y / scale.Y) // Move to correct slot
                .RotateYDeg(block.Shape.rotateY);                       // Rotate according to block

            tfMatrices[index] = matrixf.Values;
        }

        return tfMatrices;
    }

    private static float GetMeshXZSize(MeshData mesh)
    {
        Vec3f min = new(float.MaxValue, 0, float.MaxValue);
        Vec3f max = new(float.MinValue, 0, float.MinValue);
        for (int i = 0; i < mesh.VerticesCount; i++)
        {
            int index = i * 3;
            float x = mesh.xyz[index];
            float z = mesh.xyz[index + 2];
            min.X = Math.Min(min.X, x);
            min.Z = Math.Min(min.Z, z);
            max.X = Math.Max(max.X, x);
            max.Z = Math.Max(max.Z, z);
        }
        return Math.Max(max.X - min.X, max.Z - min.Z);
    }
}

internal record SurfacePosTransform(float X, float Y, float scale);
