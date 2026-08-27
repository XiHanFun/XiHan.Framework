// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 灰度规则模型测试
/// </summary>
/// <remarks>
/// GrayRule 是配置中心/数据库落库的规则载体，这里锁死默认值语义、接口契约与 JSON 往返结构。
/// </remarks>
public class GrayRuleTests
{
    /// <summary>
    /// 新建规则默认处于「未启用、最高优先级数值 0、无任何目标」的安全状态
    /// </summary>
    /// <remarks>
    /// 默认未启用很关键：漏填 IsEnabled 的规则不应该自动参与线上分流。
    /// </remarks>
    [Fact]
    public void NewRule_DefaultsToDisabledWithoutTargets()
    {
        var rule = new GrayRule();

        Assert.False(rule.IsEnabled);
        Assert.Equal(0, rule.Priority);
        Assert.Null(rule.TargetVersion);
        Assert.Null(rule.TargetServiceId);
        Assert.Null(rule.Configuration);
        Assert.Null(rule.EffectiveTime);
        Assert.Null(rule.ExpiryTime);
        Assert.Null(rule.UpdatedTime);
        Assert.Null(rule.Remark);
        Assert.Equal(default(DateTime), rule.CreatedTime);
        Assert.Equal(default(GrayRuleType), rule.RuleType);
    }

    /// <summary>
    /// GrayRule 完整实现 IGrayRule 契约
    /// </summary>
    [Fact]
    public void GrayRule_SatisfiesIGrayRuleContract()
    {
        IGrayRule rule = new GrayRule
        {
            RuleId = "rule-1",
            RuleName = "百分比灰度",
            RuleType = GrayRuleType.Percentage,
            IsEnabled = true,
            Priority = 10
        };

        Assert.Equal("rule-1", rule.RuleId);
        Assert.Equal("百分比灰度", rule.RuleName);
        Assert.Equal(GrayRuleType.Percentage, rule.RuleType);
        Assert.True(rule.IsEnabled);
        Assert.Equal(10, rule.Priority);
    }

    /// <summary>
    /// GrayRule 是普通类而非 record，相等性按引用而非按值
    /// </summary>
    /// <remarks>
    /// 引擎与仓储都按 RuleId 做键，若哪天改成 record 会让「同内容不同实例」被误判为同一条规则。
    /// </remarks>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var left = CreateRule();
        var right = CreateRule();

        Assert.NotSame(left, right);
        Assert.NotEqual(left, right);
        Assert.Equal(left, left);
    }

    /// <summary>
    /// 优先级允许负数，用于插队到默认规则之前
    /// </summary>
    [Fact]
    public void Priority_AcceptsNegativeValues()
    {
        var rule = new GrayRule { Priority = -5 };

        Assert.Equal(-5, rule.Priority);
    }

    /// <summary>
    /// JSON 往返保持规则结构、枚举数值与时间值
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesRuleContract()
    {
        var rule = new GrayRule
        {
            RuleId = "rule-1",
            RuleName = "IP 灰度",
            RuleType = GrayRuleType.IpAddress,
            IsEnabled = true,
            Priority = 5,
            TargetVersion = "v2",
            TargetServiceId = "svc-gray",
            Configuration = """{"IpAddresses":["10.0.0.1"]}""",
            CreatedTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Remark = "remark"
        };

        var json = JsonSerializer.Serialize(rule);
        var restored = JsonSerializer.Deserialize<GrayRule>(json);

        Assert.NotNull(restored);
        Assert.Equal(rule.RuleId, restored.RuleId);
        Assert.Equal(rule.RuleName, restored.RuleName);
        Assert.Equal(GrayRuleType.IpAddress, restored.RuleType);
        Assert.True(restored.IsEnabled);
        Assert.Equal(5, restored.Priority);
        Assert.Equal("v2", restored.TargetVersion);
        Assert.Equal("svc-gray", restored.TargetServiceId);
        Assert.Equal(rule.Configuration, restored.Configuration);
        Assert.Equal(rule.CreatedTime, restored.CreatedTime);
        Assert.Equal("remark", restored.Remark);
        Assert.Null(restored.EffectiveTime);
        Assert.Null(restored.ExpiryTime);
        Assert.Null(restored.UpdatedTime);
    }

    /// <summary>
    /// 规则类型按数值序列化，便于跨语言存量数据兼容
    /// </summary>
    [Fact]
    public void JsonSerialize_RuleType_IsWrittenAsNumber()
    {
        var json = JsonSerializer.Serialize(new GrayRule { RuleType = GrayRuleType.Custom });

        Assert.Contains("\"RuleType\":99", json);
    }

    /// <summary>
    /// 构造一条内容固定的规则，用于相等性对比
    /// </summary>
    private static GrayRule CreateRule()
    {
        return new GrayRule
        {
            RuleId = "rule-1",
            RuleName = "请求头灰度",
            RuleType = GrayRuleType.Header,
            IsEnabled = true,
            Priority = 1
        };
    }
}
