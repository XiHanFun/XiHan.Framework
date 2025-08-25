#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationExitEventArgs
// Guid:08014027-dfad-4bcc-b4a4-57facb79bd73
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:44:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Runtime.Enums;

namespace XiHan.Framework.Utils.Runtime.Events;

/// <summary>
/// 应用程序退出事件参数
/// </summary>
public class ApplicationExitEventArgs : EventArgs
{
    /// <summary>
    /// 退出时间
    /// </summary>
    public DateTime ExitTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 退出原因
    /// </summary>
    public ApplicationExitReason Reason { get; set; }

    /// <summary>
    /// 退出代码
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 退出消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 相关异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 是否可以取消退出
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// 是否取消退出
    /// </summary>
    public bool Cancel { get; set; }
}
