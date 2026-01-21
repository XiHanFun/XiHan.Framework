#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SignalRConstants
// Guid:af1a2b3c-4d5e-4f6a-9b2c-ad3e4f5a6b7c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 5:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.RealTime.Constants;

/// <summary>
/// SignalR 常量
/// </summary>
public static class SignalRConstants
{
    /// <summary>
    /// 客户端方法名
    /// </summary>
    public static class ClientMethods
    {
        /// <summary>
        /// 接收消息
        /// </summary>
        public const string ReceiveMessage = "ReceiveMessage";

        /// <summary>
        /// 接收通知
        /// </summary>
        public const string ReceiveNotification = "ReceiveNotification";

        /// <summary>
        /// 用户加入
        /// </summary>
        public const string UserJoined = "UserJoined";

        /// <summary>
        /// 用户离开
        /// </summary>
        public const string UserLeft = "UserLeft";

        /// <summary>
        /// 连接成功
        /// </summary>
        public const string Connected = "Connected";

        /// <summary>
        /// 断开连接
        /// </summary>
        public const string Disconnected = "Disconnected";

        /// <summary>
        /// 错误
        /// </summary>
        public const string Error = "Error";
    }

    /// <summary>
    /// 服务端方法名
    /// </summary>
    public static class ServerMethods
    {
        /// <summary>
        /// 发送消息
        /// </summary>
        public const string SendMessage = "SendMessage";

        /// <summary>
        /// 发送消息给用户
        /// </summary>
        public const string SendMessageToUser = "SendMessageToUser";

        /// <summary>
        /// 发送消息给所有人
        /// </summary>
        public const string SendMessageToAll = "SendMessageToAll";

        /// <summary>
        /// 加入组
        /// </summary>
        public const string JoinGroup = "JoinGroup";

        /// <summary>
        /// 离开组
        /// </summary>
        public const string LeaveGroup = "LeaveGroup";

        /// <summary>
        /// 发送消息给组
        /// </summary>
        public const string SendMessageToGroup = "SendMessageToGroup";

        /// <summary>
        /// 获取在线用户数量
        /// </summary>
        public const string GetOnlineUserCount = "GetOnlineUserCount";

        /// <summary>
        /// 检查用户是否在线
        /// </summary>
        public const string IsUserOnline = "IsUserOnline";
    }

    /// <summary>
    /// 组名
    /// </summary>
    public static class Groups
    {
        /// <summary>
        /// 管理员组
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// 用户组
        /// </summary>
        public const string Users = "Users";

        /// <summary>
        /// 通知组
        /// </summary>
        public const string Notifications = "Notifications";
    }

    /// <summary>
    /// Hub 路径
    /// </summary>
    public static class HubPaths
    {
        /// <summary>
        /// 通知 Hub
        /// </summary>
        public const string Notification = "/hubs/notification";

        /// <summary>
        /// 聊天 Hub
        /// </summary>
        public const string Chat = "/hubs/chat";

        /// <summary>
        /// 数据 Hub
        /// </summary>
        public const string Data = "/hubs/data";
    }
}
