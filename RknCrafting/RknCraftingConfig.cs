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
    public float BaseCraftingTimeSeconds = 1.0f;
    [ProtoMember(2)]
    public int AutoDeleteTimeSeconds = 120;
    [ProtoMember(3)]
    public float ConsecutiveCraftingTimeModifier = 0.95f;
    [ProtoMember(4)]
    public float ConsecutiveCraftingTimeModifierMin = 0.5f;
    [ProtoMember(5)]
    public bool EnableBulkCrafting = false;
    [ProtoMember(6)]
    public float BulkBaseCraftingTimeSeconds = 2.0f;
    [ProtoMember(7)]
    public bool DisableUICraftingGrid = true;

    public override string ToString()
    {
        return JObject.FromObject(this).ToString();
    }
}
