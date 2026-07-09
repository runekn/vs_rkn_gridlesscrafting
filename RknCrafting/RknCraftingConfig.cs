using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RknCrafting;

[ProtoContract]
public class RknCraftingConfig
{
    [ProtoMember(1)]
    public bool EnableBulkCrafting = false;
    [ProtoMember(2)]
    public float BulkBaseCraftingTimeSeconds = 2.0f;
    [ProtoMember(3)]
    public float BaseCraftingTimeSeconds = 1.0f;
    [ProtoMember(4)]
    public int AutoDeleteTimeSeconds = 120;
    [ProtoMember(5)]
    public float ConsecutiveCraftingTimeModifier = 0.95f;
    [ProtoMember(6)]
    public float ConsecutiveCraftingTimeModifierMin = 0.5f;
    [ProtoMember(7)]
    public bool DisableUICraftingGrid = true;

    public override string ToString()
    {
        return JObject.FromObject(this).ToString();
    }
}
