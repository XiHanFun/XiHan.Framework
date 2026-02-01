#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandContext
// Guid:4e142c60-0630-4948-90e2-3342ea77cb58
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:01:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Arguments;

namespace XiHan.Framework.DevTools.CommandLine.Commands;

/// <summary>
/// 命令执行上下文
/// </summary>
public class CommandContext
{
    /// <summary>
    /// 创建命令执行上下文
    /// </summary>
    /// <param name="arguments">命令行参数</param>
    /// <param name="parsedArguments">解析后的参数</param>
    /// <param name="commandDescriptor">命令描述符</param>
    /// <param name="output">输出流</param>
    /// <param name="error">错误输出流</param>
    /// <param name="input">输入流</param>
    /// <param name="cancellationToken">取消令牌</param>
    public CommandContext(
        string[] arguments,
        ParsedArguments parsedArguments,
        CommandDescriptor commandDescriptor,
        TextWriter? output = null,
        TextWriter? error = null,
        TextReader? input = null,
        CancellationToken cancellationToken = default)
    {
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        ParsedArguments = parsedArguments ?? throw new ArgumentNullException(nameof(parsedArguments));
        CommandDescriptor = commandDescriptor ?? throw new ArgumentNullException(nameof(commandDescriptor));
        Output = output ?? Console.Out;
        Error = error ?? Console.Error;
        Input = input ?? Console.In;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 控制台输出
    /// </summary>
    public TextWriter Output { get; }

    /// <summary>
    /// 控制台错误输出
    /// </summary>
    public TextWriter Error { get; }

    /// <summary>
    /// 控制台输入
    /// </summary>
    public TextReader Input { get; }

    /// <summary>
    /// 命令行参数
    /// </summary>
    public string[] Arguments { get; }

    /// <summary>
    /// 解析后的参数
    /// </summary>
    public ParsedArguments ParsedArguments { get; }

    /// <summary>
    /// 命令描述符
    /// </summary>
    public CommandDescriptor CommandDescriptor { get; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object> Data { get; } = [];
}
