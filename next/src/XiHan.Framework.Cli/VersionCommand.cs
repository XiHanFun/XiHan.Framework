// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 打印框架版本。
/// </summary>
internal sealed class VersionCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "version";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.VersionDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Output.WriteLineAsync($"{FrameworkInfo.Name} {FrameworkInfo.Version}".AsMemory(), context.CancellationToken).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
