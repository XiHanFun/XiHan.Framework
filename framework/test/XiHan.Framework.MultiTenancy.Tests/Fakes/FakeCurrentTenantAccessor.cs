// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Tests.Fakes;

/// <summary>
/// 当前租户访问器的手写替身
/// </summary>
/// <remarks>
/// 用普通字段承载当前租户，刻意不带 AsyncLocal 语义。
/// 这样 <see cref="CurrentTenant"/> 的作用域进出、嵌套还原等逻辑可以在不受执行上下文影响的前提下被单独验证，
/// 而真正的 AsyncLocal 传播契约留给 <see cref="AsyncLocalCurrentTenantAccessor"/> 自己的用例覆盖。
/// </remarks>
internal sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
{
    /// <summary>
    /// 当前租户
    /// </summary>
    public BasicTenantInfo? Current { get; set; }
}
