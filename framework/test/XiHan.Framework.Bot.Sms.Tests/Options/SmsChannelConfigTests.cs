// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Sms.Enums;
using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Tests.Options;

/// <summary>
/// <see cref="SmsChannelConfig"/> 短信通道配置测试
/// </summary>
/// <remarks>
/// 该类型没有 Validate 方法，校验散落在网关解析器里，因此这里只锁默认值语义与可空性契约：
/// 必填串默认空串（而非 null）、可选项默认 null、IsEnabled 默认 true（新建配置即生效）。
/// </remarks>
public class SmsChannelConfigTests
{
    /// <summary>
    /// 新建配置的默认值：服务商为阿里云、必填串为空串、可选项为 null、默认启用
    /// </summary>
    [Fact]
    public void Defaults_AreAliyunEmptyStringsAndEnabled()
    {
        var config = new SmsChannelConfig();

        Assert.Equal(0L, config.ConfigId);
        Assert.Equal(SmsProviderType.Aliyun, config.Provider);
        Assert.Equal(string.Empty, config.AccessKeyId);
        Assert.Equal(string.Empty, config.AccessKeySecret);
        Assert.Equal(string.Empty, config.SignName);
        Assert.Null(config.SdkAppId);
        Assert.Null(config.Region);
        Assert.Null(config.TemplateMap);
        Assert.True(config.IsEnabled);
    }

    /// <summary>
    /// 所有属性均可写，赋值后原样读回
    /// </summary>
    [Fact]
    public void Properties_AreMutableAndRoundTrip()
    {
        var config = new SmsChannelConfig
        {
            ConfigId = 1024L,
            Provider = SmsProviderType.TencentCloud,
            AccessKeyId = "AKID-x",
            AccessKeySecret = "secret-x",
            SdkAppId = "1400000000",
            SignName = "曦寒",
            Region = "ap-guangzhou",
            TemplateMap = """{"login":{"templateCode":"1234","paramOrder":["code"]}}""",
            IsEnabled = false
        };

        Assert.Equal(1024L, config.ConfigId);
        Assert.Equal(SmsProviderType.TencentCloud, config.Provider);
        Assert.Equal("AKID-x", config.AccessKeyId);
        Assert.Equal("secret-x", config.AccessKeySecret);
        Assert.Equal("1400000000", config.SdkAppId);
        Assert.Equal("曦寒", config.SignName);
        Assert.Equal("ap-guangzhou", config.Region);
        Assert.Equal("""{"login":{"templateCode":"1234","paramOrder":["code"]}}""", config.TemplateMap);
        Assert.False(config.IsEnabled);
    }

    /// <summary>
    /// JSON 往返保持字段名与全部取值，枚举按数值序列化
    /// </summary>
    /// <remarks>
    /// 配置会被应用层落库/回传，字段名与枚举的数值表示属于对外契约，改名或改成字符串枚举都会破坏存量数据。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_KeepsFieldNamesAndNumericProvider()
    {
        var config = new SmsChannelConfig
        {
            ConfigId = 7L,
            Provider = SmsProviderType.TencentCloud,
            AccessKeyId = "AKID-x",
            AccessKeySecret = "secret-x",
            SdkAppId = "1400000000",
            SignName = "曦寒",
            Region = "ap-guangzhou",
            TemplateMap = """{"login":{"templateCode":"1234"}}""",
            IsEnabled = true
        };

        var json = JsonSerializer.Serialize(config);

        Assert.Contains("\"ConfigId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AccessKeyId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AccessKeySecret\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SdkAppId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SignName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Region\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TemplateMap\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IsEnabled\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Provider\":1", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<SmsChannelConfig>(json);

        Assert.NotNull(restored);
        Assert.Equal(config.ConfigId, restored!.ConfigId);
        Assert.Equal(config.Provider, restored.Provider);
        Assert.Equal(config.AccessKeyId, restored.AccessKeyId);
        Assert.Equal(config.AccessKeySecret, restored.AccessKeySecret);
        Assert.Equal(config.SdkAppId, restored.SdkAppId);
        Assert.Equal(config.SignName, restored.SignName);
        Assert.Equal(config.Region, restored.Region);
        Assert.Equal(config.TemplateMap, restored.TemplateMap);
        Assert.True(restored.IsEnabled);
    }

    /// <summary>
    /// 可空属性显式置 null 后 JSON 往返仍为 null
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsNullOptionalFields()
    {
        var json = JsonSerializer.Serialize(new SmsChannelConfig());

        var restored = JsonSerializer.Deserialize<SmsChannelConfig>(json);

        Assert.NotNull(restored);
        Assert.Null(restored!.SdkAppId);
        Assert.Null(restored.Region);
        Assert.Null(restored.TemplateMap);
    }
}
