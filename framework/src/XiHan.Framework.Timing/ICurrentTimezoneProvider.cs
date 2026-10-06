// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 当前时区提供器
/// </summary>
/// <remarks>
/// 承载当前异步流（通常是一次请求）的用户时区，供 <see cref="IClock"/> 在 UTC 与用户时间之间换算。
/// </remarks>
public interface ICurrentTimezoneProvider
{
    /// <summary>
    /// 当前异步流的时区（Windows 时区 ID 或 IANA 时区名），为 <see langword="null"/> 或空白时不做时区换算
    /// </summary>
    /// <remarks>
    /// 赋值对当前异步流及其后续派生的子流（<c>await</c> 的下游、<c>Task.Run</c> 等）可见；
    /// 子流内的赋值不回流到父流，并行的异步流互不影响。
    /// 在被 <c>await</c> 的异步方法内赋值，方法返回后调用方恢复为原值；
    /// 在同一个方法内赋值则一直生效到被再次覆盖，需要临时切换时应自行保存原值并在结束时写回。
    /// </remarks>
    string? TimeZone { get; set; }
}
