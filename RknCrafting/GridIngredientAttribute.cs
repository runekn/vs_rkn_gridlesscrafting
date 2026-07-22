using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RknCrafting;

public class GridIngredientAttribute : IAttribute
{
    private CraftingRecipeIngredient? value;
    private IWorldAccessor? resolver;

    public GridIngredientAttribute()
    {
    }

    public GridIngredientAttribute(CraftingRecipeIngredient? value, IWorldAccessor resolver)
    {
      this.value = value;
      this.resolver = resolver;
    }

    public int GetAttributeId() => 8347; // Random number I hope will be okay

    public object? GetValue() => value!;
    
    public CraftingRecipeIngredient? GetIngredient() => value;

    public void SetValue(CraftingRecipeIngredient newval) => value = newval;

    public void FromBytes(BinaryReader stream)
    {
      if (stream.ReadBoolean())
        return;
      value = new CraftingRecipeIngredient();
      value.FromBytes(stream, resolver);
      value.Resolve(resolver, "GridIngredientAttribute", null); // recipe=null will ignore AddToFastSearchRecipes in harmony patch
    }

    public void ToBytes(BinaryWriter stream)
    {
      stream.Write(false);
      value.ToBytes(stream);
    }

    public bool Equals(IWorldAccessor worldForResolve, IAttribute attr)
    {
      return Equals(worldForResolve, attr, null);
    }

    internal bool Equals(IWorldAccessor worldForResolve, IAttribute attr, string[] ignorePaths)
    {
      if (attr is not GridIngredientAttribute ingredientAttribute)
        return false;
      if (ingredientAttribute.value == null && value == null)
        return true;
      return false; // TODO: eh?
    }

    public string ToJsonToken()
    {
      return ""; // TODO: eh?
    }

    public override int GetHashCode()
    {
      return value == null ? 0 : value.GetHashCode();
    }

    public IAttribute Clone() => new GridIngredientAttribute(value?.Clone(), resolver);

    Type IAttribute.GetType() => GetType();
}