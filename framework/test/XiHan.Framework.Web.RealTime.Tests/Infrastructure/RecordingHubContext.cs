// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 记录寻址与发送的 Hub 上下文替身
/// </summary>
/// <typeparam name="THub">Hub 类型</typeparam>
public sealed class RecordingHubContext<THub> : IHubContext<THub>
    where THub : Hub
{
    /// <summary>
    /// 客户端集合记录器
    /// </summary>
    public RecordingHubClients ClientsRecorder { get; } = new();

    /// <summary>
    /// 组管理记录器
    /// </summary>
    public RecordingGroupManager GroupsRecorder { get; } = new();

    /// <summary>
    /// 客户端集合
    /// </summary>
    public IHubClients Clients => ClientsRecorder;

    /// <summary>
    /// 组管理器
    /// </summary>
    public IGroupManager Groups => GroupsRecorder;
}
