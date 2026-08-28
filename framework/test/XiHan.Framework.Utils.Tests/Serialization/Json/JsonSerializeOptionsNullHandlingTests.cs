// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonSerializeOptions.IgnoreNullValues 落地测试
/// </summary>
/// <remarks>
/// 修复前 ToSystemOptions() 完全没读 IgnoreNullValues，Compact 与 WebApi 预设里设的 true 形同虚设，
/// 调用方以为 null 字段被裁掉、实际仍然全量输出。这里既锁死映射本身，
/// 也锁死"为 false 时不得反过来覆盖调用方显式设置的忽略条件"这条边界。
/// </remarks>
public class JsonSerializeOptionsNullHandlingTests
{
    /// <summary>
    /// IgnoreNullValues 为 true 时映射为写入时忽略 null
    /// </summary>
    [Fact]
    public void ToSystemOptions_WhenIgnoreNullValuesTrue_MapsToWhenWritingNull()
    {
        var system = new JsonSerializeOptions { IgnoreNullValues = true }.ToSystemOptions();

        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, system.DefaultIgnoreCondition);
    }

    /// <summary>
    /// IgnoreNullValues 为 false 时保留调用方显式设置的忽略条件
    /// </summary>
    [Fact]
    public void ToSystemOptions_WhenIgnoreNullValuesFalse_KeepsExplicitIgnoreCondition()
    {
        var options = new JsonSerializeOptions
        {
            IgnoreNullValues = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        Assert.Equal(JsonIgnoreCondition.WhenWritingDefault, options.ToSystemOptions().DefaultIgnoreCondition);
    }

    /// <summary>
    /// 两者都未设置时忽略条件保持默认的 Never
    /// </summary>
    [Fact]
    public void ToSystemOptions_WithDefaults_KeepsNeverIgnoreCondition()
    {
        Assert.Equal(JsonIgnoreCondition.Never, new JsonSerializeOptions().ToSystemOptions().DefaultIgnoreCondition);
    }

    /// <summary>
    /// 打开 IgnoreNullValues 后 null 字段不再出现在输出里
    /// </summary>
    [Fact]
    public void Serialize_WhenIgnoreNullValues_DropsNullProperties()
    {
        var options = new JsonSerializeOptions { WriteIndented = false, IgnoreNullValues = true };

        var json = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒", Nickname = null, Address = null }, options);

        Assert.DoesNotContain("nickname", json);
        Assert.DoesNotContain("address", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"tags\"", json);
    }

    /// <summary>
    /// 关闭 IgnoreNullValues 时 null 字段照常输出
    /// </summary>
    [Fact]
    public void Serialize_WhenIgnoreNullValuesFalse_KeepsNullProperties()
    {
        var options = new JsonSerializeOptions { WriteIndented = false, IgnoreNullValues = false };

        var json = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒" }, options);

        Assert.Contains("\"nickname\":null", json);
    }

    /// <summary>
    /// 紧凑预设声明的 IgnoreNullValues = true 真正生效
    /// </summary>
    [Fact]
    public void Serialize_WithCompactPreset_DropsNullProperties()
    {
        var json = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒" }, JsonSerializeOptions.Compact);

        Assert.DoesNotContain("nickname", json);
        Assert.DoesNotContain("address", json);
        Assert.Contains("\"name\"", json);
    }

    /// <summary>
    /// WebApi 预设声明的 IgnoreNullValues = true 真正生效
    /// </summary>
    [Fact]
    public void Serialize_WithWebApiPreset_DropsNullProperties()
    {
        var json = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒", Age = 18 }, JsonSerializeOptions.WebApi);

        Assert.DoesNotContain("nickname", json);
        Assert.DoesNotContain("address", json);
        Assert.Contains("\"name\"", json);
    }

    /// <summary>
    /// 格式化与严格两个预设声明的 IgnoreNullValues = false 同样被尊重
    /// </summary>
    [Fact]
    public void Serialize_WithNonIgnoringPresets_KeepsNullProperties()
    {
        var formatted = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒" }, JsonSerializeOptions.Formatted);
        var strict = JsonHelper.Serialize(new JsonSampleUser { Name = "曦寒" }, JsonSerializeOptions.Strict);

        Assert.Contains("nickname", formatted);
        Assert.Contains("Nickname", strict);
    }
}
