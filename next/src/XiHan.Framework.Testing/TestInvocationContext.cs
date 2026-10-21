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
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// 创建上下文；未提供服务提供器时使用空提供器。
    /// </summary>
    public static InvocationContext Create(
        IServiceProvider? services = null,
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
        => App.CreateContext(services ?? EmptyServiceProvider.Instance, cancellationToken, deadline: deadline, baggage: baggage);
}
