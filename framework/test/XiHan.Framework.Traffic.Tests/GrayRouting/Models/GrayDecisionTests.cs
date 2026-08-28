// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Models;

/// <summary>
/// 灰度决策测试
/// </summary>
/// <remarks>
/// 决策对象是网关侧的最终输出，两个静态工厂的默认 Reason 文案会被日志与排障直接引用，因此一并锁死。
/// </remarks>
public class GrayDecisionTests
{
    /// <summary>
    /// NotGray 不传原因时使用默认文案，且不填充任何灰度目标
    /// </summary>
    [Fact]
    public void NotGray_WithoutReason_UsesDefaultReasonAndLeavesTargetsNull()
    {
        var decision = GrayDecision.NotGray();

        Assert.False(decision.IsGray);
        Assert.Equal("未命中任何灰度规则", decision.Reason);
        Assert.Null(decision.TargetVersion);
        Assert.Null(decision.TargetServiceId);
        Assert.Null(decision.MatchedRuleId);
        Assert.Null(decision.ExtensionData);
    }

    /// <summary>
    /// NotGray 传入原因时保留调用方文案
    /// </summary>
    [Fact]
    public void NotGray_WithReason_KeepsCallerReason()
    {
        var decision = GrayDecision.NotGray("没有启用的灰度规则");

        Assert.False(decision.IsGray);
        Assert.Equal("没有启用的灰度规则", decision.Reason);
    }

    /// <summary>
    /// NotGray 的默认文案只在 reason 为 null 时生效，空串会被原样保留
    /// </summary>
    /// <remarks>
    /// 实现用的是 ?? 而非 IsNullOrEmpty，这里锁死该边界，避免以后改成 IsNullOrEmpty 时无人察觉。
    /// </remarks>
    [Fact]
    public void NotGray_WithEmptyReason_KeepsEmptyStringInsteadOfDefault()
    {
        Assert.Equal(string.Empty, GrayDecision.NotGray(string.Empty).Reason);
    }

    /// <summary>
    /// Gray 工厂填充目标版本与命中规则，并按规则ID拼默认原因
    /// </summary>
    [Fact]
    public void Gray_WithRuleId_FillsTargetVersionAndComposesDefaultReason()
    {
        var decision = GrayDecision.Gray("v2", "rule-1");

        Assert.True(decision.IsGray);
        Assert.Equal("v2", decision.TargetVersion);
        Assert.Equal("rule-1", decision.MatchedRuleId);
        Assert.Equal("命中灰度规则: rule-1", decision.Reason);
    }

    /// <summary>
    /// Gray 显式传入原因时覆盖默认拼接文案
    /// </summary>
    [Fact]
    public void Gray_WithExplicitReason_OverridesComposedReason()
    {
        var decision = GrayDecision.Gray("v2", "rule-1", "命中规则: 请求头灰度");

        Assert.Equal("命中规则: 请求头灰度", decision.Reason);
    }

    /// <summary>
    /// Gray 只传目标版本时命中规则ID保持为 null
    /// </summary>
    [Fact]
    public void Gray_WithOnlyTargetVersion_LeavesMatchedRuleIdNull()
    {
        var decision = GrayDecision.Gray("v2");

        Assert.True(decision.IsGray);
        Assert.Equal("v2", decision.TargetVersion);
        Assert.Null(decision.MatchedRuleId);
        Assert.Equal("命中灰度规则: ", decision.Reason);
    }

    /// <summary>
    /// 两个静态工厂都返回全新实例，不共享状态
    /// </summary>
    [Fact]
    public void Factories_ReturnFreshInstances()
    {
        Assert.NotSame(GrayDecision.NotGray(), GrayDecision.NotGray());
        Assert.NotSame(GrayDecision.Gray("v2"), GrayDecision.Gray("v2"));
    }

    /// <summary>
    /// 手工构造的决策完整满足 IGrayDecision 契约
    /// </summary>
    [Fact]
    public void GrayDecision_SatisfiesIGrayDecisionContract()
    {
        IGrayDecision decision = new GrayDecision
        {
            IsGray = true,
            TargetVersion = "v2",
            TargetServiceId = "svc-gray",
            MatchedRuleId = "rule-1",
            Reason = "手工构造",
            ExtensionData = new Dictionary<string, object> { ["weight"] = 30 }
        };

        Assert.True(decision.IsGray);
        Assert.Equal("v2", decision.TargetVersion);
        Assert.Equal("svc-gray", decision.TargetServiceId);
        Assert.Equal("rule-1", decision.MatchedRuleId);
        Assert.Equal("手工构造", decision.Reason);
        Assert.NotNull(decision.ExtensionData);
        Assert.Equal(30, (int)decision.ExtensionData["weight"]);
    }

    /// <summary>
    /// JSON 往返保持 Pascal 命名与可空语义
    /// </summary>
    /// <remarks>
    /// 决策会被网关按默认 JsonSerializerOptions 落到诊断日志，属性名一旦改成 camelCase 就是破坏性变更。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_PreservesPascalCasePropertyNames()
    {
        var decision = new GrayDecision
        {
            IsGray = true,
            TargetVersion = "v2",
            TargetServiceId = "svc-gray",
            MatchedRuleId = "rule-1",
            Reason = "hit"
        };

        var json = JsonSerializer.Serialize(decision);

        Assert.Contains("\"IsGray\":true", json);
        Assert.Contains("\"TargetVersion\":\"v2\"", json);
        Assert.Contains("\"TargetServiceId\":\"svc-gray\"", json);
        Assert.Contains("\"MatchedRuleId\":\"rule-1\"", json);

        var restored = JsonSerializer.Deserialize<GrayDecision>(json);

        Assert.NotNull(restored);
        Assert.True(restored.IsGray);
        Assert.Equal("v2", restored.TargetVersion);
        Assert.Equal("svc-gray", restored.TargetServiceId);
        Assert.Equal("rule-1", restored.MatchedRuleId);
        Assert.Equal("hit", restored.Reason);
        Assert.Null(restored.ExtensionData);
    }

    /// <summary>
    /// 未命中决策序列化后仍带出全部 null 字段，便于下游按固定结构解析
    /// </summary>
    [Fact]
    public void JsonSerialize_NotGrayDecision_EmitsNullTargets()
    {
        var json = JsonSerializer.Serialize(GrayDecision.NotGray("x"));

        Assert.Contains("\"IsGray\":false", json);
        Assert.Contains("\"TargetVersion\":null", json);
        Assert.Contains("\"MatchedRuleId\":null", json);
    }
}
