// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Tests.Fakes;
using SampleDomain.Entities;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 默认实体审计上下文提供器测试
/// </summary>
/// <remarks>
/// 两块逻辑各有取舍点：
/// <list type="bullet">
///   <item>基础记录填充——未认证时不写用户身份，用户名缺失时回落到 <c>Name</c>，
///         租户以用户声明优先、当前租户兜底，且租户填充不受认证状态限制（匿名请求也可能已解析出租户）；</item>
///   <item>审计范围判定——按 <c>FullName</c> 排除框架审计自身，避免「记录审计日志的动作又被审计」造成自激。</item>
/// </list>
/// 排除判定用的样例实体刻意放在 <c>SampleDomain.Entities</c> 命名空间：测试工程默认命名空间以
/// <c>XiHan.Framework.Auditing</c> 开头，会先被前缀规则命中，无法单独验证关键字分支。
/// </remarks>
public class DefaultEntityAuditContextProviderTests
{
    /// <summary>
    /// 已认证时写入用户标识与用户名
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenAuthenticated_FillsUserIdentity()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 7,
            UserName = "tom",
            Name = "Tom Smith"
        });

        var record = provider.CreateBaseRecord();

        Assert.Equal(7, record.UserId);
        Assert.Equal("tom", record.UserName);
    }

    /// <summary>
    /// 用户名缺失时回落到显示名，不留空
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenUserNameMissing_FallsBackToName()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 8,
            UserName = null,
            Name = "Tom Smith"
        });

        var record = provider.CreateBaseRecord();

        Assert.Equal(8, record.UserId);
        Assert.Equal("Tom Smith", record.UserName);
    }

    /// <summary>
    /// 未认证时不写用户身份，但租户信息仍照常填充
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenNotAuthenticated_LeavesUserIdentityEmptyButKeepsTenant()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser
        {
            IsAuthenticated = false,
            UserId = 7,
            UserName = "tom",
            TenantId = 5
        });

        var record = provider.CreateBaseRecord();

        Assert.Null(record.UserId);
        Assert.Null(record.UserName);

        // 租户解析独立于认证（匿名请求也可能已按域名/请求头解析出租户）
        Assert.Equal(5, record.TenantId);
    }

    /// <summary>
    /// 用户声明中的租户优先于当前租户上下文
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenUserHasTenant_PrefersUserTenant()
    {
        var provider = new DefaultEntityAuditContextProvider(
            new FakeCurrentUser { IsAuthenticated = true, UserId = 1, TenantId = 100 },
            new FakeCurrentTenant { Id = 200 });

        var record = provider.CreateBaseRecord();

        Assert.Equal(100, record.TenantId);
    }

    /// <summary>
    /// 用户没有租户声明时回落到当前租户上下文
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenUserHasNoTenant_FallsBackToCurrentTenant()
    {
        var provider = new DefaultEntityAuditContextProvider(
            new FakeCurrentUser { IsAuthenticated = true, UserId = 1, TenantId = null },
            new FakeCurrentTenant { Id = 200 });

        var record = provider.CreateBaseRecord();

        Assert.Equal(200, record.TenantId);
    }

    /// <summary>
    /// 未提供当前租户（构造参数可选）且用户无租户声明时租户为空，不抛空引用
    /// </summary>
    [Fact]
    public void CreateBaseRecord_WhenTenantProviderOmitted_LeavesTenantNull()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 1,
            TenantId = null
        });

        var record = provider.CreateBaseRecord();

        Assert.Null(record.TenantId);
    }

    /// <summary>
    /// 每次创建返回全新实例，调用方填充字段不会互相污染
    /// </summary>
    [Fact]
    public void CreateBaseRecord_ReturnsNewInstanceEveryTime()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser());

        var first = provider.CreateBaseRecord();
        var second = provider.CreateBaseRecord();

        Assert.NotSame(first, second);
        Assert.Equal("EntityChange", first.AuditType);
        Assert.Equal("EntityChange", second.AuditType);
    }

    /// <summary>
    /// 实体类型为空时不审计，而不是抛空引用
    /// </summary>
    [Fact]
    public void ShouldAudit_WhenTypeIsNull_ReturnsFalse()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser());

        Assert.False(provider.ShouldAudit(null!));
    }

    /// <summary>
    /// 框架审计自身的类型不审计，避免审计动作自激
    /// </summary>
    [Fact]
    public void ShouldAudit_WhenFrameworkAuditingType_ReturnsFalse()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser());

        Assert.False(provider.ShouldAudit(typeof(EntityDiffLogRecord)));
        Assert.False(provider.ShouldAudit(typeof(AccessLogRecord)));
    }

    /// <summary>
    /// 类型全名含 AuditLog / DiffLog 的实体不审计（应用侧的审计日志表本身）
    /// </summary>
    [Fact]
    public void ShouldAudit_WhenTypeNameCarriesAuditKeyword_ReturnsFalse()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser());

        Assert.False(provider.ShouldAudit(typeof(SampleAuditLogEntity)));
        Assert.False(provider.ShouldAudit(typeof(SampleEntityDiffLogEntity)));
    }

    /// <summary>
    /// 普通业务实体正常审计
    /// </summary>
    [Fact]
    public void ShouldAudit_WhenBusinessEntity_ReturnsTrue()
    {
        var provider = new DefaultEntityAuditContextProvider(new FakeCurrentUser());

        Assert.True(provider.ShouldAudit(typeof(SampleOrder)));
        Assert.True(provider.ShouldAudit(typeof(string)));
    }
}
