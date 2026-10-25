// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// <c>graph</c>：打印能力图谱（与启动日志同一份输出，<see cref="App.DescribeGraph"/>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class GraphCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "graph";

    /// <inheritdoc />
    public string DescriptionKey => CommandLineMessages.GraphDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Output.WriteLineAsync(context.Services.GetRequiredService<App>().DescribeGraph()).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
