// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 以命令行方式运行宿主：启动（装配并启动应用），执行一条命令，停止，返回退出码。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineHostExtensions
{
    /// <summary>
    /// 启动宿主、执行 <paramref name="args"/> 对应的命令、停止宿主。
    /// </summary>
    /// <remarks>
    /// Ctrl+C 取消当前命令（退出码 <see cref="CommandLineExitCodes.Cancelled"/>）。
    /// </remarks>
    public static async Task<int> RunXiHanCommandLineAsync(this IHost host, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Ctrl+C 先取消命令，让它按取消语义收尾，而不是直接终止进程
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            await host.StartAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                return await host.Services.GetRequiredService<CommandLineRunner>().RunAsync(args, Console.Out, Console.Error, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                // 停止不受命令取消影响：特性要按顺序正常关闭
                await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }
}
