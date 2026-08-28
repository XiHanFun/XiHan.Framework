// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 共享 AsyncLocal 租户上下文的测试集合
/// </summary>
/// <remarks>
/// <see cref="AsyncLocalCurrentTenantAccessor.Instance"/> 是进程级单例，
/// 凡是直接读写它的用例都归到这个集合里串行执行，避免并行调度让不同用例互相观测到对方的租户上下文。
/// 集合内的用例仍然把所有写入放进 <c>Task.Run</c> 的独立执行上下文，双保险。
/// </remarks>
[CollectionDefinition(Name)]
public class TenantContextCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "AsyncLocal 租户上下文";
}
