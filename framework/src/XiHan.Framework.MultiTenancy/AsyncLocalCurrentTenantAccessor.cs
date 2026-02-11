#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncLocalCurrentTenantAccessor
// Guid:29db82e1-2ab2-45da-84df-5de0e0eabc95
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 06:32:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
