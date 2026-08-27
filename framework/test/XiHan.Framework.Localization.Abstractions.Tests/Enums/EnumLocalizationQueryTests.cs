// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Localization.Abstractions.Enums;

namespace XiHan.Framework.Localization.Abstractions.Tests.Enums;

/// <summary>
/// 枚举本地化查询参数测试
/// </summary>
/// <remarks>
/// 这是一个会被前端以查询串/JSON 形式送进来的入参模型，
/// 其中 Ordered 的默认值是 true（与 bool 的语言默认值相反），一旦被改成 false 会静默改变列表顺序，必须锁死。
/// </remarks>
public class EnumLocalizationQueryTests
{
    /// <summary>
    /// 默认查询：不指定文化、不含隐藏项、按顺序返回
    /// </summary>
    [Fact]
    public void Constructor_Defaults_OrderedIsTrueAndOthersAreOff()
    {
        var query = new EnumLocalizationQuery();

        Assert.Null(query.CultureName);
        Assert.False(query.IncludeHidden);
        Assert.True(query.Ordered);
    }

    /// <summary>
    /// 三个属性都可写
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var query = new EnumLocalizationQuery
        {
            CultureName = "zh-CN",
            IncludeHidden = true,
            Ordered = false
        };

        Assert.Equal("zh-CN", query.CultureName);
        Assert.True(query.IncludeHidden);
        Assert.False(query.Ordered);
    }

    /// <summary>
    /// JSON 往返保留全部字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var query = new EnumLocalizationQuery
        {
            CultureName = "en-US",
            IncludeHidden = true,
            Ordered = false
        };

        var json = JsonSerializer.Serialize(query);
        var restored = JsonSerializer.Deserialize<EnumLocalizationQuery>(json);

        Assert.NotNull(restored);
        Assert.Equal("en-US", restored!.CultureName);
        Assert.True(restored.IncludeHidden);
        Assert.False(restored.Ordered);
    }

    /// <summary>
    /// 序列化使用与属性同名的字段名，前端契约不得漂移
    /// </summary>
    [Fact]
    public void JsonSerialize_UsesPropertyNamesAsIs()
    {
        var json = JsonSerializer.Serialize(new EnumLocalizationQuery { CultureName = "zh-CN" });

        Assert.Contains("\"CultureName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IncludeHidden\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Ordered\"", json, StringComparison.Ordinal);
    }
}
