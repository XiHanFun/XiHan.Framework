// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 帮助文本：用法行、命令列表（按名字排序）与操作简写的说明，文案按当前区域渲染。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineHelp
{
    /// <summary>
    /// 写帮助。
    /// </summary>
    public static async Task WriteAsync(TextWriter writer, IEnumerable<ICommand> commands)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(commands);
        await writer.WriteLineAsync(CommandLineMessages.Usage.L(("program", ProgramName()))).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync(CommandLineMessages.Commands.L()).ConfigureAwait(false);
        foreach (var command in commands.OrderBy(static c => c.Name, StringComparer.Ordinal))
        {
            await writer.WriteLineAsync($"  {command.Name,-14}{command.DescriptionKey.L()}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync("  " + CommandLineMessages.OperationShorthand.L()).ConfigureAwait(false);
    }

    /// <summary>
    /// 用法行里的程序名：可执行文件名，取不到时用进程友好名。
    /// </summary>
    private static string ProgramName()
    {
        var path = Environment.ProcessPath;
        return string.IsNullOrEmpty(path) ? AppDomain.CurrentDomain.FriendlyName : Path.GetFileNameWithoutExtension(path);
    }
}
