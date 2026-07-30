// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本执行事件参数
/// </summary>
public class ScriptExecutionEventArgs : EventArgs
{
    /// <summary>
    /// 初始化脚本执行事件参数
    /// </summary>
    /// <param name="log">执行日志</param>
    public ScriptExecutionEventArgs(ScriptExecutionLog log)
    {
        Log = log;
    }

    /// <summary>
    /// 执行日志
    /// </summary>
    public ScriptExecutionLog Log { get; }
}
