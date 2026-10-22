// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 启动子进程并把它的输出接到给定的写入器；测试用假实现记录参数。
/// </summary>
internal interface IProcessLauncher
{
    /// <summary>
    /// 运行到结束，返回退出码；无法启动时返回 <see cref="CliErrors.DotnetUnavailable"/>。
    /// </summary>
    ValueTask<Result<int>> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TextWriter output, TextWriter error, CancellationToken cancellationToken);
}
