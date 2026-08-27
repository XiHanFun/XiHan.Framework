// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 固定租户信息的当前租户替身
/// </summary>
/// <remarks>
/// 灰度中间件只读取 <see cref="ICurrentTenant.Id"/>，其余成员保持最小可用实现。
/// </remarks>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">租户标识，传空表示无租户</param>
    /// <param name="name">租户名称</param>
    public FakeCurrentTenant(long? id = null, string? name = null)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户标识
    /// </summary>
    public long? Id { get; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 临时切换租户，替身不做任何切换
    /// </summary>
    public IDisposable Change(long? id, string? name = null)
    {
        return new EmptyScope();
    }

    /// <summary>
    /// 空作用域
    /// </summary>
    private sealed class EmptyScope : IDisposable
    {
        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
        }
    }
}
