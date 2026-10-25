// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Hosting.CommandLine.Parsing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// 一次命令执行的环境：解析后的命令行、本次执行的服务作用域、标准输出与标准错误、取消令牌。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CommandContext
{
    /// <summary>
    /// 创建。
    /// </summary>
    public CommandContext(ParsedCommandLine commandLine, IServiceProvider services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        CommandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Error = error ?? throw new ArgumentNullException(nameof(error));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 解析后的命令行。
    /// </summary>
    public ParsedCommandLine CommandLine { get; }

    /// <summary>
    /// 命令名之后的位置参数。
    /// </summary>
    public IReadOnlyList<string> Arguments => CommandLine.Arguments;

    /// <summary>
    /// 本次执行的服务作用域。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 标准输出：命令的数据输出（JSON、文本）。
    /// </summary>
    public TextWriter Output { get; }

    /// <summary>
    /// 标准错误：失败与诊断。
    /// </summary>
    public TextWriter Error { get; }

    /// <summary>
    /// 取消令牌（Ctrl+C 或宿主停止）。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 选项的最后一个值。
    /// </summary>
    public Maybe<string> Option(string name) => CommandLine.Option(name);

    /// <summary>
    /// 是否给出了该选项。
    /// </summary>
    public bool HasOption(string name) => CommandLine.HasOption(name);
}
