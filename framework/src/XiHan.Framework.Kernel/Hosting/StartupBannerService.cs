// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 在宿主启动时打印启动 Banner
/// </summary>
/// <remarks>
/// 打印放在 <see cref="StartingAsync"/>：它在所有宿主服务的 <c>StartAsync</c> 之前执行，Banner 排在特性启动日志与 Web 服务器的监听日志前面，与 4.x 在应用创建时打印的位置一致。选项在这时才读取，命令行宿主在装配之后换掉的选项也生效
/// </remarks>
/// <param name="services">根服务提供器</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "由容器通过 AddHostedService 实例化。")]
internal sealed class StartupBannerService(IServiceProvider services) : IHostedLifecycleService
{
    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        // 遵循 no-color.org 约定：NO_COLOR 设了非空值就不上色
        Write(Console.Out, Console.IsOutputRedirected, Console.Error, Console.IsErrorRedirected, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 按选项打印 Banner
    /// </summary>
    /// <remarks>
    /// 控制台的写入器、重定向状态与 <c>NO_COLOR</c> 由调用方传入，测试不必改进程的控制台与环境变量
    /// </remarks>
    /// <param name="standardOutput">标准输出，选项没有指定目标时写这里</param>
    /// <param name="outputRedirected">标准输出是否被重定向</param>
    /// <param name="standardError">标准错误</param>
    /// <param name="errorRedirected">标准错误是否被重定向</param>
    /// <param name="noColor">是否设置了 <c>NO_COLOR</c></param>
    internal void Write(TextWriter standardOutput, bool outputRedirected, TextWriter standardError, bool errorRedirected, bool noColor)
    {
        var options = services.GetRequiredService<StartupBannerOptions>();
        if (!options.Enabled)
        {
            return;
        }

        var output = options.Output ?? standardOutput;
        var environment = services.GetService<IHostEnvironment>();
        var text = StartupBanner.Render(environment?.ApplicationName, options.ApplicationVersion, environment?.EnvironmentName, services.GetService<IMessageResolver>(), !noColor && UseColor(output, standardOutput, outputRedirected, standardError, errorRedirected));
        // 整段一次写出：控制台写入器按调用加锁，Banner 不会被并发的日志行拆开
        output.Write(text);
        output.Flush();
    }

    /// <summary>
    /// 是否给 Logo 上色
    /// </summary>
    /// <remarks>
    /// 只有写到没有重定向的控制台才上色：写到文件、管道或容器日志时转义序列会成乱码。应用指定的其他写入器一律不上色
    /// </remarks>
    private static bool UseColor(TextWriter output, TextWriter standardOutput, bool outputRedirected, TextWriter standardError, bool errorRedirected)
    {
        if (ReferenceEquals(output, standardOutput))
        {
            return !outputRedirected;
        }

        return ReferenceEquals(output, standardError) && !errorRedirected;
    }
}
