// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli;

using var cancellation = new CancellationTokenSource();
// Ctrl+C 先取消当前命令，让它按取消语义收尾，而不是直接终止进程
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await CliHost.RunAsync(args, Console.Out, Console.Error, cancellation.Token).ConfigureAwait(false);

/// <summary>
/// 工具入口
/// </summary>
/// <remarks>
/// 显式声明为内部类型：引用 ASP.NET Core 共享框架后，它的源生成器会把顶级语句的 <c>Program</c> 生成为公开类型
/// </remarks>
internal sealed partial class Program;
