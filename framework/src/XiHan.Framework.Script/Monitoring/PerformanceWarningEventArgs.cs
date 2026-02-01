#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceWarningEventArgs
// Guid:e272c119-a0ed-4d36-87d1-918934237344
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:08:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 性能警告事件参数
/// </summary>
public class PerformanceWarningEventArgs : EventArgs
{
    /// <summary>
    /// 初始化性能警告事件参数
    /// </summary>
    /// <param name="log">执行日志</param>
    /// <param name="warnings">警告信息列表</param>
    public PerformanceWarningEventArgs(ScriptExecutionLog log, IReadOnlyList<string> warnings)
    {
        Log = log;
        Warnings = warnings;
    }

    /// <summary>
    /// 执行日志
    /// </summary>
    public ScriptExecutionLog Log { get; }

    /// <summary>
    /// 警告信息列表
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }
}
