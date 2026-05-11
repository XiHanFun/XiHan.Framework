// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace XiHan.Framework.Kernel.Hosting.Tests;

public class ExternalDiTests
{
    [Fact]
    public void ExternalMode_ShouldShareServices()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var builder = new XiHanAppBuilder(services, config);
        builder.UseFeature<TestFeature>();
        var app = builder.Build();

        Assert.Null(app.ServiceProvider);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<TestService>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void ExternalMode_FeatureServices_ShouldBeInHostDi()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var builder = new XiHanAppBuilder(services, config);
        builder.UseFeature<TestFeature>();
        builder.Build();

        var provider = services.BuildServiceProvider();
        var svc = provider.GetService<TestService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void StandaloneMode_ServiceProvider_ShouldNotBeNull()
    {
        var builder = new XiHanAppBuilder();
        builder.UseFeature<TestFeature>();
        var app = builder.Build();

        Assert.NotNull(app.ServiceProvider);
        Assert.NotNull(app.ServiceProvider.GetService<TestService>());
    }

    private sealed class TestFeature : IXiHanFeature
    {
        public string Name => "Test";
        public void Configure(IFeatureConfigurationContext ctx)
            => ctx.Services.AddSingleton<TestService>();
    }

    private sealed class TestService { }
}
