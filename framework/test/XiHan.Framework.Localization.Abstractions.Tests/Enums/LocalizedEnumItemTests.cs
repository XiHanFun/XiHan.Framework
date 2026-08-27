// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Localization.Abstractions.Enums;

namespace XiHan.Framework.Localization.Abstractions.Tests.Enums;

/// <summary>
/// 枚举项本地化描述测试
/// </summary>
/// <remarks>
/// 这是直接下发给前端的传输契约：字符串字段承诺"非空但可能为空串"，可空字段承诺"可能不存在"，
/// 两类语义不同，前端据此决定是否兜底，因此默认值和 JSON 字段名一并锁死。
/// </remarks>
public class LocalizedEnumItemTests
{
    /// <summary>
    /// 默认值：必填文本为空串，可选文本为 null，Value 未赋值时为 null
    /// </summary>
    [Fact]
    public void Constructor_Defaults_RequiredTextsAreEmptyAndOptionalsAreNull()
    {
        var item = new LocalizedEnumItem();

        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(string.Empty, item.ValueText);
        Assert.Equal(string.Empty, item.Label);
        Assert.Equal(string.Empty, item.Description);
        Assert.Null(item.Value);
        Assert.Null(item.Theme);
        Assert.Null(item.Icon);
        Assert.Null(item.ResourceName);
        Assert.Null(item.LocalizationKey);
        Assert.Null(item.Extra);
        Assert.Equal(0, item.Order);
        Assert.False(item.Hidden);
        Assert.False(item.Disabled);
    }

    /// <summary>
    /// JSON 往返保留标量字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarProperties()
    {
        var item = new LocalizedEnumItem
        {
            Name = "Active",
            Value = 1,
            ValueText = "1",
            Label = "启用",
            Description = "启用状态",
            Theme = "success",
            Icon = "check",
            Order = 10,
            Hidden = true,
            Disabled = true,
            ResourceName = "Enums",
            LocalizationKey = "Status.Active"
        };

        var json = JsonSerializer.Serialize(item);
        var restored = JsonSerializer.Deserialize<LocalizedEnumItem>(json);

        Assert.NotNull(restored);
        Assert.Equal("Active", restored!.Name);
        Assert.Equal("1", restored.ValueText);
        Assert.Equal("启用", restored.Label);
        Assert.Equal("启用状态", restored.Description);
        Assert.Equal("success", restored.Theme);
        Assert.Equal("check", restored.Icon);
        Assert.Equal(10, restored.Order);
        Assert.True(restored.Hidden);
        Assert.True(restored.Disabled);
        Assert.Equal("Enums", restored.ResourceName);
        Assert.Equal("Status.Active", restored.LocalizationKey);
    }

    /// <summary>
    /// Value 是 object，序列化后按原始 JSON 数值下发，反序列化回来是 JsonElement
    /// </summary>
    [Fact]
    public void JsonRoundTrip_ValueIsWrittenAsRawJsonNumber()
    {
        var json = JsonSerializer.Serialize(new LocalizedEnumItem { Name = "Active", Value = 1 });

        Assert.Contains("\"Value\":1", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<LocalizedEnumItem>(json);

        Assert.NotNull(restored);
        var value = Assert.IsType<JsonElement>(restored!.Value);
        Assert.Equal(1, value.GetInt32());
    }

    /// <summary>
    /// 扩展字段在 JSON 往返后保留键集合
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesExtraDictionaryKeys()
    {
        var item = new LocalizedEnumItem
        {
            Name = "Active",
            Extra = new Dictionary<string, object>
            {
                ["max"] = 10,
                ["group"] = "status"
            }
        };

        var json = JsonSerializer.Serialize(item);
        var restored = JsonSerializer.Deserialize<LocalizedEnumItem>(json);

        Assert.NotNull(restored);
        var extra = restored!.Extra;
        Assert.NotNull(extra);
        Assert.Equal(2, extra!.Count);
        Assert.Contains("max", extra.Keys);
        Assert.Contains("group", extra.Keys);
    }

    /// <summary>
    /// 序列化使用与属性同名的字段名，前端契约不得漂移
    /// </summary>
    [Fact]
    public void JsonSerialize_UsesPropertyNamesAsIs()
    {
        var json = JsonSerializer.Serialize(new LocalizedEnumItem { Name = "Active", Label = "启用" });

        Assert.Contains("\"Name\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ValueText\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Label\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Description\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Order\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Hidden\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Disabled\"", json, StringComparison.Ordinal);
        Assert.Contains("\"LocalizationKey\"", json, StringComparison.Ordinal);
    }
}
