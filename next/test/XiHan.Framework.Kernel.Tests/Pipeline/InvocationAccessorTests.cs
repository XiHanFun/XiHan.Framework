// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// 当前调用的上下文访问器：调用内可取、嵌套调用各看到自己的、调用结束后恢复。
/// </summary>
public sealed class InvocationAccessorTests
{
    [Fact]
    public async Task Current_IsAvailableInsideInvocation_StacksForNestedCalls_AndClearsAfterwards()
    {
        var (provider, app) = Hosts.BuildApp(static _ => { });
        using var _ = provider;
        await app.StartAsync(CancellationToken.None);
        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IInvocationAccessor>();
        Assert.False(accessor.Current.HasValue);

        var outer = Hosts.Context(scope.ServiceProvider);
        InvocationContext? seenOuter = null;
        InvocationContext? seenInner = null;
        InvocationContext? seenAfterInner = null;
        var result = await app.InvokeAsync(outer, async context =>
        {
            seenOuter = accessor.Current.ValueOrThrow();
            // 同一作用域内嵌套一次调用：内层看到派生的上下文，返回后外层恢复
            await app.InvokeAsync(context.WithBaggage(BaggageKeys.Correlation, "inner"), _ =>
            {
                seenInner = accessor.Current.ValueOrThrow();
                return ValueTask.CompletedTask;
            });
            seenAfterInner = accessor.Current.ValueOrThrow();
        });

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Same(outer, seenOuter);
        Assert.NotSame(outer, seenInner);
        Assert.Equal("inner", seenInner!.Baggage[BaggageKeys.Correlation]);
        Assert.Same(outer, seenAfterInner);
        Assert.False(accessor.Current.HasValue);
    }

    [Fact]
    public void Current_IsScoped_EachScopeHasItsOwnAccessor()
    {
        var (provider, _) = Hosts.BuildApp(static _ => { });
        using var __ = provider;
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.NotSame(first.ServiceProvider.GetRequiredService<IInvocationAccessor>(), second.ServiceProvider.GetRequiredService<IInvocationAccessor>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IInvocationAccessor>(), first.ServiceProvider.GetRequiredService<IInvocationAccessor>());
    }
}
