#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentTimezoneProvider
// Guid:e582c150-951d-47bc-9681-a62d15f240ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:24:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Timing;

/// <summary>
/// 当前时区提供器
/// </summary>
public class CurrentTimezoneProvider : ICurrentTimezoneProvider, ISingletonDependency
{
    private readonly AsyncLocal<string?> _currentScope;

    /// <summary>
    /// 构造函数
    /// </summary>
    public CurrentTimezoneProvider()
    {
        _currentScope = new AsyncLocal<string?>();
    }

    /// <summary>
    /// 当前时区
    /// </summary>
    public string? TimeZone
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }
}
