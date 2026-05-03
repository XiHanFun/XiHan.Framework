using XiHan.Framework.Bootstrap.Abstractions.Application;
using XiHan.Framework.Kernel.DependencyInjection;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Hosts.Api;

/// <summary>
/// 表示 API 宿主启动模块。
/// </summary>
public sealed class ApiHostModule : XiHanModule, IOnApplicationInitialization
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOpenApi();
    }

    /// <inheritdoc />
    public void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var applicationAccessor = context.ServiceProvider.GetRequiredService<IObjectAccessor<WebApplication>>();
        var app = applicationAccessor.Value ?? throw new InvalidOperationException("当前未能获取 WebApplication 实例。");

        app.MapGet("/", () => Results.Ok(new
        {
            Name = "XiHan.Framework.Next API Host",
            Mode = "Bootstrap + Module Runtime",
            Time = DateTimeOffset.UtcNow
        }))
        .WithName("HostRoot");

        app.MapGet("/framework/bootstrap", () => Results.Ok(new
        {
            Message = "宿主已通过 Bootstrap 初始化完成。",
            Architecture = "Kernel + Bootstrap.Abstractions + Bootstrap",
            Module = nameof(ApiHostModule)
        }))
        .WithName("BootstrapStatus");
    }

    /// <inheritdoc />
    public Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        OnApplicationInitialization(context);
        return Task.CompletedTask;
    }
}
