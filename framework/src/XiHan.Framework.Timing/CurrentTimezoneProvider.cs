// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 当前时区提供器
/// </summary>
/// <remarks>
/// 时区存放在静态 <see cref="AsyncLocal{T}"/> 中，归属于当前异步流而不是实例：
/// 同一异步流里任意实例读到的都是同一个值，因此业务代码写入的时区对单例 <see cref="IClock"/> 可见。
/// </remarks>
public class CurrentTimezoneProvider : ICurrentTimezoneProvider
{
    private static readonly AsyncLocal<string?> CurrentScope = new();

    /// <summary>
    /// 当前异步流的时区
    /// </summary>
    public string? TimeZone
    {
        get => CurrentScope.Value;
        set => CurrentScope.Value = value;
    }
}
