// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 一个命令行命令。以 <c>AddXiHanCommand&lt;TCommand&gt;()</c> 注册为单例；执行时得到本次执行的作用域与输出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICommand
{
    /// <summary>
    /// 命令名（如 <c>graph</c>）；全局唯一。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 说明文案的消息键（RFC-0010）；帮助里以当前区域渲染。
    /// </summary>
    string DescriptionKey { get; }

    /// <summary>
    /// 执行并返回退出码（见 <see cref="CommandLineExitCodes"/>）。
    /// </summary>
    ValueTask<int> ExecuteAsync(CommandContext context);
}
