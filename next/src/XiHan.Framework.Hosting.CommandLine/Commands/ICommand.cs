// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// 一个命令行命令。
/// </summary>
/// <remarks>
/// 以 <c>AddXiHanCommand&lt;TCommand&gt;()</c> 注册为单例；执行时得到本次执行的作用域与输出。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICommand
{
    /// <summary>
    /// 命令名（如 <c>graph</c>）。
    /// </summary>
    /// <remarks>
    /// 全局唯一。
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// 说明文案的消息键（RFC-0010）。
    /// </summary>
    /// <remarks>
    /// 帮助里以当前区域渲染。
    /// </remarks>
    string DescriptionKey { get; }

    /// <summary>
    /// 执行并返回退出码（见 <see cref="CommandLineExitCodes"/>）。
    /// </summary>
    ValueTask<int> ExecuteAsync(CommandContext context);
}
