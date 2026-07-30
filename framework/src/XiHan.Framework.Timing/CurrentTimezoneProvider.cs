// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 当前时区提供器
/// </summary>
public class CurrentTimezoneProvider : ICurrentTimezoneProvider
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
