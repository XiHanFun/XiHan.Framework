// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Tests.Fakes;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 空实体审计上下文提供器测试
/// </summary>
/// <remarks>
/// 空提供器的语义是「整体关闭实体变更审计」：任何类型都不审计，且每次取到的基础记录都是全新的空白实例——
/// 若它返回共享实例，调用方填充字段会互相污染。
/// </remarks>
public class NullEntityAuditContextProviderTests
{
    /// <summary>
    /// 创建的基础记录不带任何上下文，仅保留模型自身的默认值
    /// </summary>
    [Fact]
    public void CreateBaseRecord_LeavesContextFieldsEmpty()
    {
        var provider = new NullEntityAuditContextProvider();

        var record = provider.CreateBaseRecord();

        Assert.Equal("EntityChange", record.AuditType);
        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
        Assert.Null(record.TenantId);
        Assert.Null(record.RequestPath);
        Assert.Null(record.RequestMethod);
        Assert.Null(record.OperationIp);
        Assert.Null(record.RequestId);
    }

    /// <summary>
    /// 每次创建返回全新实例，调用方填充字段不会互相污染
    /// </summary>
    [Fact]
    public void CreateBaseRecord_ReturnsNewInstanceEveryTime()
    {
        var provider = new NullEntityAuditContextProvider();

        var first = provider.CreateBaseRecord();
        var second = provider.CreateBaseRecord();

        Assert.NotSame(first, second);

        first.EntityId = "1";

        Assert.Null(second.EntityId);
    }

    /// <summary>
    /// 任何实体类型都不审计
    /// </summary>
    [Fact]
    public void ShouldAudit_ForAnyType_ReturnsFalse()
    {
        var provider = new NullEntityAuditContextProvider();

        Assert.False(provider.ShouldAudit(typeof(SampleOrder)));
        Assert.False(provider.ShouldAudit(typeof(string)));
        Assert.False(provider.ShouldAudit(typeof(EntityDiffLogRecord)));
    }
}
