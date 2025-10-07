using Microsoft.Extensions.Hosting;
using Serilog;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Hosting;
using XiHan.Framework.Integration.Tests;
using XiHan.Framework.Tests.Integration;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    await builder.Services.AddApplicationAsync<XiHanTestsIntegrationModule>();

    var host = builder.Build();

    await host.InitializeAsync();

    Region.Main();

    await host.RunAsync();

    Log.Information("应用启动");
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用关闭");
}
finally
{
    await Log.CloseAndFlushAsync();
}
