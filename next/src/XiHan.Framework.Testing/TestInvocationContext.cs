// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 不依赖宿主的调用上下文。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class TestInvocationContext
{
    /// <summary>
    /// 什么都解析不到的服务提供器。
    /// </summary>
    /// <remarks>
    /// 供不关心服务解析的测试使用。
    /// </remarks>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>
        /// 共享实例。
        /// </summary>
        /// <remarks>
        /// 无状态，可跨测试复用。
        /// </remarks>
        public static EmptyServiceProvider Instance { get; } = new();

        /// <summary>
        /// 任何服务类型都返回 <c>null</c>。
        /// </summary>
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// 创建上下文。
    /// </summary>
    /// <remarks>
    /// 未提供服务提供器时使用空提供器。
    /// </remarks>
    public static InvocationContext Create(
        IServiceProvider? services = null,
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
        => App.CreateContext(services ?? EmptyServiceProvider.Instance, cancellationToken, deadline: deadline, baggage: baggage);
}
