#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LifecycleEventArgs
// Guid:68003a20-382b-40c4-b4a3-dcc7b8cf6e58
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:43:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Runtime.Enums;

namespace XiHan.Framework.Utils.Runtime.Events;

/// <summary>
/// 生命周期事件参数
/// </summary>
public class LifecycleEventArgs : EventArgs
{
    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 应用程序状态
    /// </summary>
    public ApplicationState State { get; set; }

    /// <summary>
    /// 前一个状态
    /// </summary>
    public ApplicationState PreviousState { get; set; }

    /// <summary>
    /// 消息描述
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 相关异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 是否可以取消（仅适用于某些事件）
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// 是否取消操作
    /// </summary>
    public bool Cancel { get; set; }
}
