// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing.Tests.Fakes;

/// <summary>
/// 当前时区提供器的手写替身
/// </summary>
/// <remarks>
/// 用普通自动属性承载时区，绕开真实实现的 AsyncLocal 流转语义，
/// 让时钟的分支断言不受测试执行上下文影响。
/// </remarks>
public sealed class FakeCurrentTimezoneProvider : ICurrentTimezoneProvider
{
    /// <summary>
    /// 当前时区
    /// </summary>
    public string? TimeZone { get; set; }
}
