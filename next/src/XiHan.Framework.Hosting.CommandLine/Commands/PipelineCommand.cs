// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// <c>pipeline</c>：打印管道的阶段与中间件（<see cref="App.DescribePipeline"/>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PipelineCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "pipeline";

    /// <inheritdoc />
    public string DescriptionKey => CommandLineMessages.PipelineDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Output.WriteLineAsync(context.Services.GetRequiredService<App>().DescribePipeline()).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
