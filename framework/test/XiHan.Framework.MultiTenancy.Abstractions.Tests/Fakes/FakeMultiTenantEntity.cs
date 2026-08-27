// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 多租户实体的手写替身
/// </summary>
/// <remarks>
/// 只承载 <see cref="IMultiTenant.TenantId"/> 一个契约字段，用来验证「null 表示宿主数据」的过滤语义。
/// </remarks>
internal sealed class FakeMultiTenantEntity : IMultiTenant
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tenantId">所属租户唯一标识，null 表示宿主（Host）数据</param>
    public FakeMultiTenantEntity(long? tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// 所属租户唯一标识
    /// </summary>
    public long? TenantId { get; }
}
