// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 当前租户访问器的手写替身
/// </summary>
/// <remarks>
/// 抽象包内只有契约没有实现，测试所需的最小实现一律手写，不引入替身框架。
/// 这里刻意用最朴素的自动属性，保证「访问器只是一个可读可写的槽位」这一语义不被实现细节干扰。
/// </remarks>
internal sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
{
    /// <summary>
    /// 当前租户
    /// </summary>
    public BasicTenantInfo? Current { get; set; }
}
