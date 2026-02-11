#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WebSocketEventArgs
// Guid:cf5fe3b7-e469-40ce-880e-11b0484aca9f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 15:29:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.WebSockets;

namespace XiHan.Framework.Utils.Net.WebSocket;

/// <summary>
/// WebSocket事件参数
/// </summary>
public class WebSocketEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">消息内容</param>
    public WebSocketEventArgs(string message)
    {
        Message = message;
    }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// WebSocket错误事件参数
/// </summary>
public class WebSocketErrorEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="exception">异常</param>
    public WebSocketErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; }
}

/// <summary>
/// WebSocket关闭事件参数
/// </summary>
public class WebSocketCloseEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="closeStatus">关闭状态码</param>
    /// <param name="closeStatusDescription">关闭描述</param>
    /// <param name="isRemoteClose">是否通过远程关闭</param>
    public WebSocketCloseEventArgs(WebSocketCloseStatus closeStatus, string? closeStatusDescription, bool isRemoteClose)
    {
        CloseStatus = closeStatus;
        CloseStatusDescription = closeStatusDescription;
        IsRemoteClose = isRemoteClose;
    }

    /// <summary>
    /// 关闭状态码
    /// </summary>
    public WebSocketCloseStatus CloseStatus { get; }

    /// <summary>
    /// 关闭描述
    /// </summary>
    public string? CloseStatusDescription { get; }

    /// <summary>
    /// 是否通过远程关闭
    /// </summary>
    public bool IsRemoteClose { get; }
}
