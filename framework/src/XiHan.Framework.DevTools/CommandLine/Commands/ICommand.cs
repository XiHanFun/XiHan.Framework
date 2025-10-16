#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICommand
// Guid:k7l8m902-j4l1-3728-hm6k-i8fd84c18144
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
