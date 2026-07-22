// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 基于AsyncLocal的当前租户访问器
/// </summary>
public class AsyncLocalCurrentTenantAccessor : ICurrentTenantAccessor
{
    private readonly AsyncLocal<BasicTenantInfo?> _currentScope;

    /// <summary>
    /// 构造函数
    /// </summary>
    private AsyncLocalCurrentTenantAccessor()
    {
        _currentScope = new AsyncLocal<BasicTenantInfo?>();
    }

    /// <summary>
    /// 单例实例
    /// </summary>
    public static AsyncLocalCurrentTenantAccessor Instance { get; } = new();

    /// <summary>
    /// 当前租户
    /// </summary>
    public BasicTenantInfo? Current
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }
}
