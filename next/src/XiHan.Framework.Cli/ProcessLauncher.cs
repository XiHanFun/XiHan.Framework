// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel;
using System.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 以 <see cref="Process"/> 启动子进程，标准输出与标准错误逐行转发到写入器；取消时结束子进程。
/// </summary>
internal sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc />
    public async ValueTask<Result<int>> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                return Error.Unavailable(CliErrors.DotnetUnavailable, CliMessages.DotnetUnavailable, MessageArguments.Of(("reason", fileName)));
            }
        }
        catch (Win32Exception exception)
        {
            return Error.Unavailable(CliErrors.DotnetUnavailable, CliMessages.DotnetUnavailable, MessageArguments.Of(("reason", exception.Message)));
        }

        // 两路输出并行转发，避免某一路缓冲区满把子进程卡住
        var pumpOutput = PumpAsync(process.StandardOutput, output, cancellationToken);
        var pumpError = PumpAsync(process.StandardError, error, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 已经退出
            }

            throw;
        }

        await Task.WhenAll(pumpOutput, pumpError).ConfigureAwait(false);
        return Result.Success(process.ExitCode);
    }

    /// <summary>
    /// 逐行把一路输出转发到写入器。
    /// </summary>
    private static async Task PumpAsync(StreamReader reader, TextWriter writer, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }
}
