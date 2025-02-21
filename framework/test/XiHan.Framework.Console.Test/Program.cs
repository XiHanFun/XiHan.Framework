using Serilog;
using XiHan.Framework.AspNetCore.Extensions.Builder;
using XiHan.Framework.Console.Test;
using XiHan.Framework.Core.Extensions.DependencyInjection;

try
{
    var builder = WebApplication.CreateBuilder(args);

    _ = await builder.Services.AddApplicationAsync<XiHanConsoleTestModule>();

    var app = builder.Build();

    await app.InitializeApplicationAsync();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用意外关闭");
}
finally
{
    Log.CloseAndFlush();
}
