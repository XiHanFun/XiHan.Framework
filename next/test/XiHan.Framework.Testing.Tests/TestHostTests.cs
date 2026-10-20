// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing.Tests;

public sealed class TestHostTests
{
    [Fact]
    public async Task StartAsync_StartsFeaturesAndInvokeRunsPipeline()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.test.stub", requires: ["kernel.hosting@1"]));
        await using var host = await TestHost.StartAsync(
            b => b.UseFeature(feature),
            configuration: [new KeyValuePair<string, string?>("XiHan:Kernel:StartTimeoutSeconds", "5")],
            services: s => s.AddSingleton<string>("extra"));

        Assert.Equal(["configure", "start"], feature.Events);
        Assert.Equal(TimeSpan.FromSeconds(5), host.App.Options.StartTimeout);
        Assert.Equal("extra", host.Services.GetRequiredService<string>());

        var result = await host.InvokeAsync(static context =>
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = "t-1" });

        Assert.True(result.IsSuccess);

        var expired = await host.InvokeAsync(static _ => ValueTask.CompletedTask, deadline: DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(KernelErrors.DeadlineExceeded, expired.Error!.Code);
    }

    [Fact]
    public async Task DisposeAsync_StopsFeatures()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.test.stub"));
        var host = await TestHost.StartAsync(b => b.UseFeature(feature));

        await host.DisposeAsync();
        await host.DisposeAsync();

        Assert.Equal(["configure", "start", "stop"], feature.Events);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await host.InvokeAsync(static _ => ValueTask.CompletedTask));
    }

    [Fact]
    public async Task StartAsync_FailingFeature_DisposesProviderAndThrows()
    {
        var feature = new StubFeature(TestManifests.Create("xihan.test.broken")) { OnStart = static (_, _) => throw new InvalidOperationException("boom") };

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(b => b.UseFeature(feature)));

        Assert.Equal(KernelErrors.FeatureStartFailed, exception.Code);
    }

    [Fact]
    public void TestInvocationContext_CreatesStandaloneContext()
    {
        var context = TestInvocationContext.Create(deadline: DateTimeOffset.UtcNow.AddSeconds(5));

        Assert.Equal(32, context.TraceId.Length);
        Assert.NotNull(context.Deadline);
        Assert.Null(context.Services.GetService(typeof(string)));
    }
}
