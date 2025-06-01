#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptExecutionEventArgs
// Guid:8f369667-78e8-4fe6-beb5-0ccfb3094225
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/1 11:08:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
