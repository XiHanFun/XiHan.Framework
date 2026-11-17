// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 回调元数据还原成调用上下文（<c>invocation.md</c> §2，ADR-0237）
/// </summary>
public sealed class PluginMetadataTests
{
    [Fact]
    public void CreateContext_TakesTheEarlierOfTheMetadataAndTheCallDeadline()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var metadata = new InvocationMetadata { Deadline = "2026-11-18T03:25:05Z" };
        var call = new DateTimeOffset(2026, 11, 18, 3, 25, 0, TimeSpan.Zero);

        // 修之前 Host.Invoke 与 Host.Publish 不看 gRPC 调用自身的截止时间
        Assert.Equal(call, PluginMetadata.CreateContext(services, metadata, "xihan.samples.echo", CancellationToken.None, call).Deadline);
        Assert.Equal(call, PluginMetadata.CreateContext(services, null, "xihan.samples.echo", CancellationToken.None, call).Deadline);
        Assert.Equal(call.AddSeconds(5), PluginMetadata.CreateContext(services, metadata, "xihan.samples.echo", CancellationToken.None).Deadline);
    }

    [Fact]
    public void CreateContext_IgnoresADeadlineOutsideTheGrammar()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        // 修之前 DateTimeOffset.TryParse 把它读成 UTC 03:25
        var context = PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = "2026-11-18 03:25:00" }, "xihan.samples.echo", CancellationToken.None);

        Assert.Null(context.Deadline);
    }

    [Fact]
    public void FromCall_TreatsMaxValueAsNoDeadline()
    {
        Assert.Null(PluginDeadlines.FromCall(DateTime.MaxValue));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 25, 0, TimeSpan.Zero), PluginDeadlines.FromCall(new DateTime(2026, 11, 18, 3, 25, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Enter_MakesTheContextTheCurrentInvocationOfItsScope()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        await using var scope = host.Services.CreateAsyncScope();
        var context = PluginMetadata.CreateContext(scope.ServiceProvider, new InvocationMetadata(), "xihan.samples.echo", CancellationToken.None);
        var accessor = scope.ServiceProvider.GetRequiredService<IInvocationAccessor>();

        using (PluginMetadata.Enter(context))
        {
            Assert.Same(context, accessor.Current.ValueOrThrow());
        }

        Assert.False(accessor.Current.HasValue);
    }
}
