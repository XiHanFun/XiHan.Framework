// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 测试操作执行时记下的调用来源（RFC-0003），按到达顺序。
/// </summary>
/// <remarks>
/// 宿主没有登记它时操作不记。
/// </remarks>
internal sealed class OriginLog
{
    /// <summary>
    /// 记录。
    /// </summary>
    /// <remarks>
    /// 没有来源记 <c>null</c>。
    /// </remarks>
    private readonly ConcurrentQueue<InvocationOrigin?> _origins = new();

    /// <summary>
    /// 已记录的来源快照。
    /// </summary>
    public IReadOnlyList<InvocationOrigin?> Origins => _origins.ToList();

    /// <summary>
    /// 记下调用上下文里的来源。
    /// </summary>
    public void Record(InvocationContext context) => _origins.Enqueue(context.GetOrigin().TryGetValue(out var origin) ? origin : null);
}
