// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DevTools.CommandLine.Commands;

/// <summary>
/// 命令接口
/// </summary>
public interface ICommand
{
    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>退出代码</returns>
    Task<int> ExecuteAsync(CommandContext context);
}

/// <summary>
/// 同步命令接口
/// </summary>
public interface ISyncCommand
{
    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>退出代码</returns>
    int Execute(CommandContext context);
}
