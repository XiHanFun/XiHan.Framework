using Serilog;
using XiHan.Framework.AspNetCore.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Test;

try
{
    var builder = WebApplication.CreateBuilder(args);

    _ = await builder.AddApplicationAsync<XiHanWebTestModule>();

    var app = builder.Build();

    await app.InitializeApplicationAsync();

    await app.RunAsync();

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
