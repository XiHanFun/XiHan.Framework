// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 记录组成员变更的组管理器替身
/// </summary>
public sealed class RecordingGroupManager : IGroupManager
{
    /// <summary>
    /// 已记录的入组操作（按发生顺序）
    /// </summary>
    public List<GroupMembershipChange> Added { get; } = [];

    /// <summary>
    /// 已记录的出组操作（按发生顺序）
    /// </summary>
    public List<GroupMembershipChange> Removed { get; } = [];

    /// <summary>
    /// 记录一次入组
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <param name="groupName">组名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Added.Add(new GroupMembershipChange(connectionId, groupName));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录一次出组
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <param name="groupName">组名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Removed.Add(new GroupMembershipChange(connectionId, groupName));
        return Task.CompletedTask;
    }
}
