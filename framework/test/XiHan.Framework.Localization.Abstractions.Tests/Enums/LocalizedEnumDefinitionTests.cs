// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Localization.Abstractions.Enums;

namespace XiHan.Framework.Localization.Abstractions.Tests.Enums;

/// <summary>
/// 枚举本地化描述测试
/// </summary>
/// <remarks>
/// Items 默认必须是空集合而不是 null——前端直接遍历该字段，一旦退化为 null 会在所有枚举下拉框上炸开。
/// </remarks>
public class LocalizedEnumDefinitionTests
{
    /// <summary>
    /// 默认值：文本字段为空串，可选资源名为 null，条目集合为空且非 null
    /// </summary>
    [Fact]
    public void Constructor_Defaults_ItemsIsEmptyAndTextsAreEmpty()
    {
        var definition = new LocalizedEnumDefinition();

        Assert.Equal(string.Empty, definition.EnumName);
        Assert.Equal(string.Empty, definition.FullName);
        Assert.Equal(string.Empty, definition.DisplayName);
        Assert.Equal(string.Empty, definition.CultureName);
        Assert.Equal(string.Empty, definition.UnderlyingTypeName);
        Assert.Null(definition.ResourceName);
        Assert.False(definition.IsFlags);
        Assert.NotNull(definition.Items);
        Assert.Empty(definition.Items);
    }

    /// <summary>
    /// 条目集合可用任意只读列表赋值并保持顺序
    /// </summary>
    [Fact]
    public void Items_AcceptsReadOnlyListAndKeepsOrder()
    {
        var definition = new LocalizedEnumDefinition
        {
            Items =
            [
                new LocalizedEnumItem { Name = "Active", Order = 1 },
                new LocalizedEnumItem { Name = "Disabled", Order = 2 }
            ]
        };

        Assert.Equal(2, definition.Items.Count);
        Assert.Equal("Active", definition.Items[0].Name);
        Assert.Equal("Disabled", definition.Items[1].Name);
    }

    /// <summary>
    /// JSON 往返保留描述字段与条目
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesDefinitionAndItems()
    {
        var definition = new LocalizedEnumDefinition
        {
            EnumName = "UserStatus",
            FullName = "XiHan.Demo.UserStatus",
            DisplayName = "用户状态",
            CultureName = "zh-CN",
            IsFlags = true,
            UnderlyingTypeName = "Int32",
            ResourceName = "Enums",
            Items =
            [
                new LocalizedEnumItem { Name = "Active", ValueText = "1", Label = "启用", Order = 1 }
            ]
        };

        var json = JsonSerializer.Serialize(definition);
        var restored = JsonSerializer.Deserialize<LocalizedEnumDefinition>(json);

        Assert.NotNull(restored);
        Assert.Equal("UserStatus", restored!.EnumName);
        Assert.Equal("XiHan.Demo.UserStatus", restored.FullName);
        Assert.Equal("用户状态", restored.DisplayName);
        Assert.Equal("zh-CN", restored.CultureName);
        Assert.True(restored.IsFlags);
        Assert.Equal("Int32", restored.UnderlyingTypeName);
        Assert.Equal("Enums", restored.ResourceName);
        Assert.Single(restored.Items);
        Assert.Equal("Active", restored.Items[0].Name);
        Assert.Equal("启用", restored.Items[0].Label);
        Assert.Equal(1, restored.Items[0].Order);
    }

    /// <summary>
    /// 空条目集合序列化为空数组而不是 null
    /// </summary>
    [Fact]
    public void JsonSerialize_EmptyItems_WritesEmptyArray()
    {
        var json = JsonSerializer.Serialize(new LocalizedEnumDefinition { EnumName = "UserStatus" });

        Assert.Contains("\"Items\":[]", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// 序列化使用与属性同名的字段名，前端契约不得漂移
    /// </summary>
    [Fact]
    public void JsonSerialize_UsesPropertyNamesAsIs()
    {
        var json = JsonSerializer.Serialize(new LocalizedEnumDefinition { EnumName = "UserStatus" });

        Assert.Contains("\"EnumName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"FullName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"DisplayName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"CultureName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IsFlags\"", json, StringComparison.Ordinal);
        Assert.Contains("\"UnderlyingTypeName\"", json, StringComparison.Ordinal);
    }
}
