// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Guardrails;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 护栏配置测试
/// </summary>
/// <remarks>
/// 护栏是安全策略，默认值方向与管道开关相反：内置注入启发式默认开启，
/// 部署方若什么都不配也应当拿到一道基础防线。
/// </remarks>
public class AiGuardrailOptionsTests
{
    /// <summary>
    /// 配置节名锁定为 XiHan:AI:Guardrail
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationPath()
    {
        Assert.Equal("XiHan:AI:Guardrail", AiGuardrailOptions.SectionName);
    }

    /// <summary>
    /// 护栏配置节挂在 AI 根配置节之下
    /// </summary>
    /// <remarks>
    /// 两个常量各自独立声明，容易在改名时只改一个；此处断言它们的父子关系，
    /// 保证 XiHan:AI 整体迁移时不会漏掉护栏节。
    /// </remarks>
    [Fact]
    public void SectionName_IsNestedUnderRootAiSection()
    {
        Assert.Equal(XiHanAiOptions.SectionName + ":Guardrail", AiGuardrailOptions.SectionName);
    }

    /// <summary>
    /// 内置注入启发式默认开启
    /// </summary>
    /// <remarks>安全默认必须是「开」；这条与管道里其他默认关的开关是有意相反的，不要被顺手统一。</remarks>
    [Fact]
    public void UseBuiltInInjectionHeuristics_WhenNewInstance_IsEnabled()
    {
        var options = new AiGuardrailOptions();

        Assert.True(options.UseBuiltInInjectionHeuristics);
    }

    /// <summary>
    /// 黑名单集合默认已初始化且为空
    /// </summary>
    /// <remarks>默认空表示「只跑内置启发式」，调用方直接 Add 即可，不必先判空再新建。</remarks>
    [Fact]
    public void Defaults_ForRuleLists_AreInitializedAndEmpty()
    {
        var options = new AiGuardrailOptions();

        Assert.NotNull(options.BlockedKeywords);
        Assert.Empty(options.BlockedKeywords);
        Assert.NotNull(options.InjectionPatterns);
        Assert.Empty(options.InjectionPatterns);
    }

    /// <summary>
    /// 每个实例持有各自的规则集合，不共享同一份引用
    /// </summary>
    [Fact]
    public void Defaults_ForTwoInstances_AreNotSharedReferences()
    {
        var first = new AiGuardrailOptions();
        var second = new AiGuardrailOptions();

        Assert.NotSame(first.BlockedKeywords, second.BlockedKeywords);
        Assert.NotSame(first.InjectionPatterns, second.InjectionPatterns);
    }

    /// <summary>
    /// 拒绝话术默认非空且表明请求已被安全策略拦截
    /// </summary>
    /// <remarks>
    /// 只断言语义关键词而不锁死整句：话术是面向终端用户的文案，允许润色，
    /// 但「被拦截」这个信息不能丢，否则用户会以为是模型故障而反复重试。
    /// </remarks>
    [Fact]
    public void RefusalMessage_WhenNewInstance_ExplainsBlocking()
    {
        var options = new AiGuardrailOptions();

        Assert.False(string.IsNullOrWhiteSpace(options.RefusalMessage));
        Assert.Contains("拦截", options.RefusalMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 规则条目可追加并保持插入顺序
    /// </summary>
    /// <remarks>顺序会影响命中后返回的原因文案，属可观测行为。</remarks>
    [Fact]
    public void BlockedKeywords_WhenAppended_KeepsInsertionOrder()
    {
        var options = new AiGuardrailOptions();
        options.BlockedKeywords.Add("第一条");
        options.BlockedKeywords.Add("第二条");

        Assert.Equal(2, options.BlockedKeywords.Count);
        Assert.Equal("第一条", options.BlockedKeywords[0]);
        Assert.Equal("第二条", options.BlockedKeywords[1]);
    }

    /// <summary>
    /// 全字段可经 System.Text.Json 往返且值不丢失
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithCustomRules_PreservesEveryField(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new AiGuardrailOptions
        {
            UseBuiltInInjectionHeuristics = false,
            RefusalMessage = "已拦截"
        };
        source.BlockedKeywords.Add("违禁词");
        source.InjectionPatterns.Add("(?i)ignore\\s+previous");

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<AiGuardrailOptions>(json, serializerOptions)!;

        Assert.False(restored.UseBuiltInInjectionHeuristics);
        Assert.Equal("已拦截", restored.RefusalMessage);
        Assert.Equal("违禁词", Assert.Single(restored.BlockedKeywords));
        Assert.Equal("(?i)ignore\\s+previous", Assert.Single(restored.InjectionPatterns));
    }

    /// <summary>
    /// 类型为 sealed，不允许派生出绕过规则的护栏配置
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(AiGuardrailOptions).IsSealed);
    }
}
