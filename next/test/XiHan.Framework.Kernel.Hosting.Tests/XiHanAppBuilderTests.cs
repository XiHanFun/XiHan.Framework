// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using Xunit;

namespace XiHan.Framework.Kernel.Hosting.Tests;

public class XiHanAppBuilderTests
{
    [Fact]
    public void Build_NoFeatures_ShouldCreateApp()
    {
        var app = new XiHanAppBuilder().Build();
        Assert.NotNull(app);
        Assert.NotNull(app.ServiceProvider);
    }

    [Fact]
    public void UseFeature_ShouldRegisterFeature()
    {
        var builder = new XiHanAppBuilder();
        builder.UseFeature<TestFeature>();
        Assert.Equal(1, builder.Features.Count);
    }

    [Fact]
    public void Build_WithFeature_ShouldConfigureServices()
    {
        var builder = new XiHanAppBuilder();
        builder.UseFeature<TestFeature>();
        var app = builder.Build();

        var service = app.ServiceProvider.GetService<TestService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Build_WithPipeline_ShouldSetPipeline()
    {
        var builder = new XiHanAppBuilder();
        builder.UsePipeline(p => p.Use<EchoMiddleware>());
        var app = builder.Build();

        Assert.NotNull(app.Pipeline);
    }

    [Fact]
    public void CreateBuilder_ShouldReturnBuilder()
    {
        var builder = XiHanApp.CreateBuilder();
        Assert.NotNull(builder);
    }

    [Fact]
    public async Task CreateBuilder_Build_ShouldBeDisposable()
    {
        var app = XiHanApp.CreateBuilder().Build();
        await app.DisposeAsync();
    }

    private sealed class TestFeature : IXiHanFeature
    {
        public string Name => "Test";

        public void Configure(IFeatureConfigurationContext context)
        {
            context.Services.AddSingleton<TestService>();
        }
    }

    private sealed class TestService
    { }

    private sealed class EchoMiddleware : IPipelineMiddleware
    {
        public Task InvokeAsync(PipelineContext context, PipelineDelegate next)
            => next(context);
    }
}
