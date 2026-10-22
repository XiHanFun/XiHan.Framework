// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 记录启动参数、不真的启动进程的启动器。
/// </summary>
internal sealed class RecordingLauncher : IProcessLauncher
{
    /// <summary>
    /// 要返回的退出码。
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 为真时模拟 dotnet 不可用。
    /// </summary>
    public bool Unavailable { get; set; }

    /// <summary>
    /// 最近一次的文件名。
    /// </summary>
    public string? FileName { get; private set; }

    /// <summary>
    /// 最近一次的参数。
    /// </summary>
    public IReadOnlyList<string> Arguments { get; private set; } = [];

    /// <inheritdoc />
    public async ValueTask<Result<int>> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        FileName = fileName;
        Arguments = arguments;
        if (Unavailable)
        {
            return Error.Unavailable(CliErrors.DotnetUnavailable, CliMessages.DotnetUnavailable, MessageArguments.Of(("reason", "not found")));
        }

        await output.WriteLineAsync("forwarded").ConfigureAwait(false);
        return Result.Success(ExitCode);
    }
}
