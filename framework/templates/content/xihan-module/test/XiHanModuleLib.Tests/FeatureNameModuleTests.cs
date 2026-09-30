using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Hosting;
using XiHanModuleLib.Options;
using XiHanModuleLib.Services;

namespace XiHanModuleLib.Tests;

/// <summary>
/// FeatureName 模块装配测试
/// </summary>
public class FeatureNameModuleTests
{
    [Fact]
    public async Task Module_RegistersServiceWithConfiguredOptions()
    {
        using var host = await CreateHostAsync(new Dictionary<string, string?>
        {
            [$"{FeatureNameOptions.SectionName}:{nameof(FeatureNameOptions.GreetingPrefix)}"] = "Hello"
        });

        var service = host.Services.GetRequiredService<IFeatureNameService>();

        Assert.IsType<DefaultFeatureNameService>(service);
        Assert.Equal("Hello，XiHan！", await service.GreetAsync("XiHan", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Module_UsesDefaultOptionsWithoutConfiguration()
    {
        using var host = await CreateHostAsync([]);

        var service = host.Services.GetRequiredService<IFeatureNameService>();

        Assert.Equal("你好，XiHan！", await service.GreetAsync(" XiHan ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GreetAsync_WithBlankName_Throws()
    {
        using var host = await CreateHostAsync([]);

        var service = host.Services.GetRequiredService<IFeatureNameService>();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GreetAsync("  ", TestContext.Current.CancellationToken));
    }

    private static async Task<IHost> CreateHostAsync(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);

        await builder.Services.AddApplicationAsync<FeatureNameModule>();

        var host = builder.Build();
        await host.InitializeAsync();
        return host;
    }
}
