// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.SqlSugar.Entities;
using XiHan.Framework.Traffic.SqlSugar.Mapping;

namespace XiHan.Framework.Traffic.SqlSugar.Tests;

/// <summary>
/// 灰度规则实体与映射测试
/// </summary>
public class EntityMappingTests
{
    /// <summary>
    /// 表名符合约定
    /// </summary>
    [Fact]
    public void 表名符合约定()
    {
        var table = typeof(SysGrayRule).GetCustomAttribute<SugarTable>();

        Assert.NotNull(table);
        Assert.Equal("sys_gray_rule", table.TableName);
    }

    /// <summary>
    /// 主键经构造函数传入且非自增
    /// </summary>
    [Fact]
    public void 主键经构造函数传入且非自增()
    {
        var entity = new SysGrayRule("rule-1");
        var property = typeof(SysGrayRule).GetProperty(nameof(SysGrayRule.BasicId));
        var column = property!.GetCustomAttribute<SugarColumn>();

        Assert.Equal("rule-1", entity.BasicId);
        Assert.NotNull(column);
        Assert.False(column.IsIdentity);
    }

    /// <summary>
    /// 往返映射保留全部字段，含四个时间字段
    /// </summary>
    [Fact]
    public void 往返映射保留全部字段()
    {
        var effectiveTime = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var expiryTime = effectiveTime.AddDays(7);
        var model = new GrayRule
        {
            RuleId = "rule-1",
            RuleName = "百分比灰度",
            RuleType = GrayRuleType.Percentage,
            IsEnabled = true,
            Priority = 10,
            TargetVersion = "2.0.0",
            TargetServiceId = "order-service",
            Configuration = "{\"percentage\":10}",
            EffectiveTime = effectiveTime,
            ExpiryTime = expiryTime,
            CreatedTime = effectiveTime,
            UpdatedTime = effectiveTime,
            Remark = "备注"
        };

        var entity = GrayRuleMapper.ToEntity(model);
        var roundTrip = GrayRuleMapper.ToModel(entity);

        Assert.Equal(model.RuleId, roundTrip.RuleId);
        Assert.Equal(model.RuleName, roundTrip.RuleName);
        Assert.Equal(model.RuleType, roundTrip.RuleType);
        Assert.Equal(model.IsEnabled, roundTrip.IsEnabled);
        Assert.Equal(model.Priority, roundTrip.Priority);
        Assert.Equal(model.TargetVersion, roundTrip.TargetVersion);
        Assert.Equal(model.TargetServiceId, roundTrip.TargetServiceId);
        Assert.Equal(model.Configuration, roundTrip.Configuration);
        Assert.Equal(model.Remark, roundTrip.Remark);
        Assert.Equal(effectiveTime, roundTrip.EffectiveTime);
        Assert.Equal(expiryTime, roundTrip.ExpiryTime);
        Assert.Equal(effectiveTime, roundTrip.CreatedTime);
        Assert.Equal(effectiveTime, roundTrip.UpdatedTime);
    }

    /// <summary>
    /// 未标注时区的时间按 UTC 解释，往返后保持不变
    /// </summary>
    /// <remarks>
    /// 断言 <see cref="DateTimeKind.Unspecified"/> 输入按 UTC 解释后往返，期望值固定为
    /// <see cref="DateTime.SpecifyKind(DateTime, DateTimeKind)"/> 转换后的结果。
    /// </remarks>
    [Fact]
    public void 未标注时区的时间按UTC解释往返后保持不变()
    {
        var unspecified = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Unspecified);
        var model = new GrayRule
        {
            RuleId = "rule-2",
            RuleName = "未标注时区",
            CreatedTime = unspecified,
            UpdatedTime = unspecified,
            EffectiveTime = unspecified,
            ExpiryTime = unspecified
        };

        var entity = GrayRuleMapper.ToEntity(model);
        var roundTrip = GrayRuleMapper.ToModel(entity);

        var expectedUtc = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);

        Assert.Equal(expectedUtc, roundTrip.CreatedTime);
        Assert.Equal(expectedUtc, roundTrip.UpdatedTime);
        Assert.Equal(expectedUtc, roundTrip.EffectiveTime);
        Assert.Equal(expectedUtc, roundTrip.ExpiryTime);
        Assert.Equal(DateTimeKind.Utc, roundTrip.CreatedTime.Kind);
    }

    /// <summary>
    /// 本地时间转换为同一时刻的 UTC 后往返
    /// </summary>
    [Fact]
    public void 本地时间转换为同一时刻的UTC后往返()
    {
        var local = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Local);
        var model = new GrayRule
        {
            RuleId = "rule-3",
            RuleName = "本地时间",
            CreatedTime = local,
            UpdatedTime = local,
            EffectiveTime = local,
            ExpiryTime = local
        };

        var roundTrip = GrayRuleMapper.ToModel(GrayRuleMapper.ToEntity(model));

        var expectedUtc = local.ToUniversalTime();

        Assert.Equal(expectedUtc, roundTrip.CreatedTime);
        Assert.Equal(expectedUtc, roundTrip.UpdatedTime);
        Assert.Equal(expectedUtc, roundTrip.EffectiveTime);
        Assert.Equal(expectedUtc, roundTrip.ExpiryTime);
    }
}
