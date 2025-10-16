using Microsoft.Extensions.Hosting;
using Serilog;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Hosting;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Integration.Tests;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    await builder.Services.AddApplicationAsync<XiHanTestsIntegrationModule>();

    var host = builder.Build();

    await host.InitializeAsync();

    var url = $"https://www.google.com";
    var httpResult = await url.AsHttp().GetStringAsync();

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
