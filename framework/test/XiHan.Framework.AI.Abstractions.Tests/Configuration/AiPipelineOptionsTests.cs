// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Abstractions.Tests.Configuration;

/// <summary>
/// AI 会话管道横切开关配置测试
/// </summary>
/// <remarks>
/// 这些开关决定护栏是否生效、prompt 原文是否进遥测、响应是否被缓存复用，
/// 默认值一旦漂移就是安全与隐私事故，因此逐项锁死「默认全关」这条声明。
/// </remarks>
public class AiPipelineOptionsTests
{
    /// <summary>
    /// 新实例的全部开关默认关闭
    /// </summary>
    [Fact]
    public void Defaults_WhenNewInstance_AllSwitchesAreOff()
    {
        var options = new AiPipelineOptions();

        Assert.False(options.EnableGuardrail);
        Assert.False(options.EnableTelemetry);
        Assert.False(options.EnableSensitiveTelemetry);
        Assert.False(options.EnableResponseCache);
    }

    /// <summary>
    /// 任何布尔开关默认都必须为 false
    /// </summary>
    /// <remarks>
    /// 按反射枚举而不是硬编码名单：将来新增开关若默认开启，会在此直接失败，
    /// 避免「加了个默认开的功能」悄悄改变部署方的既有行为。
    /// </remarks>
    [Fact]
    public void Defaults_ForEveryBooleanSwitch_IsFalse()
    {
        var options = new AiPipelineOptions();

        var switchedOn = typeof(AiPipelineOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(bool))
            .Where(property => (bool)property.GetValue(options)!)
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(switchedOn);
    }

    /// <summary>
    /// 遥测源名默认为 XiHan.AI
    /// </summary>
    /// <remarks>该名字是 ActivitySource/Meter 的标识，导出器与看板按它过滤，属对外契约。</remarks>
    [Fact]
    public void TelemetrySourceName_WhenNewInstance_IsXiHanAi()
    {
        var options = new AiPipelineOptions();

        Assert.Equal("XiHan.AI", options.TelemetrySourceName);
    }

    /// <summary>
    /// 敏感遥测开关独立于遥测总开关
    /// </summary>
    /// <remarks>
    /// 打开遥测不应顺带打开 prompt 原文记录；两者必须是两个独立字段，
    /// 否则「我只想看耗时」会连带把用户输入写进链路数据。
    /// </remarks>
    [Fact]
    public void EnableSensitiveTelemetry_WhenTelemetryEnabled_StaysOff()
    {
        var options = new AiPipelineOptions
        {
            EnableTelemetry = true
        };

        Assert.True(options.EnableTelemetry);
        Assert.False(options.EnableSensitiveTelemetry);
    }

    /// <summary>
    /// 全部字段可经 System.Text.Json 往返且值不丢失
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    /// <remarks>两种命名策略都要过，确认类型没有被特性钉死成某一种线上字段名。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithAllSwitchesOn_PreservesEveryField(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new AiPipelineOptions
        {
            EnableGuardrail = true,
            EnableTelemetry = true,
            EnableSensitiveTelemetry = true,
            TelemetrySourceName = "Custom.Source",
            EnableResponseCache = true
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<AiPipelineOptions>(json, serializerOptions)!;

        Assert.True(restored.EnableGuardrail);
        Assert.True(restored.EnableTelemetry);
        Assert.True(restored.EnableSensitiveTelemetry);
        Assert.True(restored.EnableResponseCache);
        Assert.Equal("Custom.Source", restored.TelemetrySourceName);
    }

    /// <summary>
    /// 类型为 sealed，不允许派生出行为不同的管道开关
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(AiPipelineOptions).IsSealed);
    }
}
