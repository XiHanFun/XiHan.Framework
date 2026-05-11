// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace XiHan.Framework.Kernel.Hosting.Tests;

public class MultiHostedFeatureTests
{
    [Fact]
    public void MultipleHostedFeatures_ShouldRegisterAll()
    {
        var builder = new XiHanAppBuilder();
        builder.Features.Add(new NamedHostedFeature("A"));
        builder.Features.Add(new NamedHostedFeature("B"));
        var app = builder.Build();

        var services = new ServiceCollection();
        services.AddHostedFeatures(app.Features);
        var provider = services.BuildServiceProvider();

        var wrappers = provider.GetServices<IHostedService>().ToList();
        Assert.Equal(2, wrappers.Count);
    }

    [Fact]
    public void SingleFeature_RegistersOneWrapper()
    {
        var builder = new XiHanAppBuilder();
        builder.Features.Add(new NamedHostedFeature("Only"));
        var app = builder.Build();

        var services = new ServiceCollection();
        services.AddHostedFeatures(app.Features);
        var provider = services.BuildServiceProvider();

        var wrapper = provider.GetService<IHostedService>();
        Assert.NotNull(wrapper);
    }

    private sealed class NamedHostedFeature : IHostedFeature
    {
        public string Name { get; }
        public NamedHostedFeature(string name) => Name = name;
        public void Configure(IFeatureConfigurationContext ctx) { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
