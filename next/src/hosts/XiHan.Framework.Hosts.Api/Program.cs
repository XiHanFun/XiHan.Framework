using XiHan.Framework.Bootstrap.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.DependencyInjection;

namespace XiHan.Framework.Hosts.Api;

/// <summary>
/// 表示 API 宿主程序入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 程序主入口。
    /// </summary>
    /// <param name="args">启动参数。</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<ObjectAccessor<WebApplication>>();
        builder.Services.AddSingleton<IObjectAccessor<WebApplication>>(serviceProvider =>
            serviceProvider.GetRequiredService<ObjectAccessor<WebApplication>>());

        var frameworkApplication = await builder.Services.AddApplicationAsync<ApiHostModule>(options =>
        {
            options.ApplicationName = "XiHan.Framework.Next API Host";
        });

        var app = builder.Build();

        app.MapOpenApi();

        app.Services.GetRequiredService<ObjectAccessor<WebApplication>>().Value = app;
        await frameworkApplication.InitializeAsync(app.Services);

        app.Lifetime.ApplicationStopping.Register(() => frameworkApplication.Shutdown());

        await app.RunAsync();
    }
}
