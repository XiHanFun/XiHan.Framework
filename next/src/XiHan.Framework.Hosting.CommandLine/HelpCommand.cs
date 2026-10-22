// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// <c>help</c>：打印用法与命令列表。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HelpCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "help";

    /// <inheritdoc />
    public string DescriptionKey => CommandLineMessages.HelpDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await CommandLineHelp.WriteAsync(context.Output, context.Services.GetServices<ICommand>()).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
