// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Constants;
using XiHan.Framework.Web.RealTime.Hubs;
using XiHan.Framework.Web.RealTime.Services;
using XiHan.Framework.Web.RealTime.Tests.Infrastructure;

namespace XiHan.Framework.Web.RealTime.Tests.Hubs;

/// <summary>
/// 通知 Hub 测试
/// </summary>
/// <remarks>
/// 直接 new Hub 并手工赋 <c>Context</c>/<c>Clients</c>/<c>Groups</c>，不起真实 SignalR 服务器。
/// 断言两件事：消息发给了正确的寻址目标，以及客户端方法名与 <see cref="SignalRConstants.ClientMethods"/> 一致。
/// </remarks>
public class NotificationHubTests
{
    /// <summary>
    /// 目标用户不在线时不发送任何消息
    /// </summary>
    [Fact]
    public async Task SendMessageToUser_WhenTargetOffline_SendsNothing()
    {
        var manager = new ConnectionManager();
        var clients = new RecordingHubClients();
        using var hub = CreateHub(manager, clients, new RecordingGroupManager());

        await hub.SendMessageToUser("目标用户", "你好");

        Assert.Empty(clients.RequestedConnectionIdBatches);
        Assert.Empty(clients.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 目标用户在线时向其全部连接发送并带上发送者 ID
    /// </summary>
    [Fact]
    public async Task SendMessageToUser_WhenTargetOnline_SendsReceiveMessageWithSenderId()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("目标用户", "target-1");
        await manager.AddConnectionAsync("目标用户", "target-2");

        var clients = new RecordingHubClients();
        using var hub = CreateHub(manager, clients, new RecordingGroupManager());

        await hub.SendMessageToUser("目标用户", "你好");

        Assert.Single(clients.RequestedConnectionIdBatches);
        var addressed = clients.RequestedConnectionIdBatches[0];
        Assert.Equal(2, addressed.Count);
        Assert.Contains("target-1", addressed);
        Assert.Contains("target-2", addressed);

        Assert.Single(clients.ClientsProxy.Invocations);
        var invocation = clients.ClientsProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.ReceiveMessage, invocation.Method);
        Assert.Equal(2, invocation.Args.Length);
        Assert.Equal("caller", (string?)invocation.Args[0]);
        Assert.Equal("你好", (string?)invocation.Args[1]);
    }

    /// <summary>
    /// 全体广播走 All 通道并带上发送者 ID
    /// </summary>
    [Fact]
    public async Task SendMessageToAll_BroadcastsReceiveMessageWithSenderId()
    {
        var clients = new RecordingHubClients();
        using var hub = CreateHub(new ConnectionManager(), clients, new RecordingGroupManager());

        await hub.SendMessageToAll("公告");

        Assert.Single(clients.AllProxy.Invocations);
        var invocation = clients.AllProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.ReceiveMessage, invocation.Method);
        Assert.Equal("caller", (string?)invocation.Args[0]);
        Assert.Equal("公告", (string?)invocation.Args[1]);
    }

    /// <summary>
    /// 匿名调用方广播时发送者 ID 为 null
    /// </summary>
    [Fact]
    public async Task SendMessageToAll_WhenCallerAnonymous_SendsNullSenderId()
    {
        var clients = new RecordingHubClients();
        using var hub = CreateHub(new ConnectionManager(), clients, new RecordingGroupManager(), userId: null);

        await hub.SendMessageToAll("公告");

        var invocation = clients.AllProxy.Invocations[0];
        Assert.Null(invocation.Args[0]);
        Assert.Equal("公告", (string?)invocation.Args[1]);
    }

    /// <summary>
    /// 加入组时把调用方连接加进组并向组内广播加入事件
    /// </summary>
    [Fact]
    public async Task JoinGroup_AddsCallerConnectionAndAnnouncesUserJoined()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        using var hub = CreateHub(new ConnectionManager(), clients, groups);

        await hub.JoinGroup(SignalRConstants.Groups.Notifications);

        Assert.Single(groups.Added);
        Assert.Equal("caller-conn", groups.Added[0].ConnectionId);
        Assert.Equal(SignalRConstants.Groups.Notifications, groups.Added[0].GroupName);
        Assert.Empty(groups.Removed);

        Assert.Single(clients.RequestedGroupNames);
        Assert.Equal(SignalRConstants.Groups.Notifications, clients.RequestedGroupNames[0]);

        Assert.Single(clients.GroupProxy.Invocations);
        var invocation = clients.GroupProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.UserJoined, invocation.Method);
        Assert.Equal("caller", (string?)invocation.Args[0]);
        Assert.Equal(SignalRConstants.Groups.Notifications, (string?)invocation.Args[1]);
    }

    /// <summary>
    /// 离开组时把调用方连接移出组并向组内广播离开事件
    /// </summary>
    [Fact]
    public async Task LeaveGroup_RemovesCallerConnectionAndAnnouncesUserLeft()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        using var hub = CreateHub(new ConnectionManager(), clients, groups);

        await hub.LeaveGroup(SignalRConstants.Groups.Notifications);

        Assert.Single(groups.Removed);
        Assert.Equal("caller-conn", groups.Removed[0].ConnectionId);
        Assert.Equal(SignalRConstants.Groups.Notifications, groups.Removed[0].GroupName);
        Assert.Empty(groups.Added);

        Assert.Single(clients.GroupProxy.Invocations);
        Assert.Equal(SignalRConstants.ClientMethods.UserLeft, clients.GroupProxy.Invocations[0].Method);
    }

    /// <summary>
    /// 组内发消息只寻址目标组且不触碰组成员关系
    /// </summary>
    [Fact]
    public async Task SendMessageToGroup_SendsToNamedGroupWithoutChangingMembership()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        using var hub = CreateHub(new ConnectionManager(), clients, groups);

        await hub.SendMessageToGroup(SignalRConstants.Groups.Admin, "运维通知");

        Assert.Empty(groups.Added);
        Assert.Empty(groups.Removed);
        Assert.Single(clients.RequestedGroupNames);
        Assert.Equal(SignalRConstants.Groups.Admin, clients.RequestedGroupNames[0]);

        var invocation = clients.GroupProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.ReceiveMessage, invocation.Method);
        Assert.Equal("caller", (string?)invocation.Args[0]);
        Assert.Equal("运维通知", (string?)invocation.Args[1]);
    }

    /// <summary>
    /// 在线用户数直接来自连接管理器
    /// </summary>
    [Fact]
    public async Task GetOnlineUserCount_ReflectsConnectionManager()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u2", "c2");
        await manager.AddConnectionAsync("u2", "c3");

        using var hub = CreateHub(manager, new RecordingHubClients(), new RecordingGroupManager());

        Assert.Equal(2, await hub.GetOnlineUserCount());
    }

    /// <summary>
    /// 在线判定直接来自连接管理器
    /// </summary>
    [Fact]
    public async Task IsUserOnline_ReflectsConnectionManager()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        using var hub = CreateHub(manager, new RecordingHubClients(), new RecordingGroupManager());

        Assert.True(await hub.IsUserOnline("u1"));
        Assert.False(await hub.IsUserOnline("u2"));
    }

    /// <summary>
    /// 通知 Hub 继承自曦寒 Hub 基类，因此复用其连接登记语义
    /// </summary>
    [Fact]
    public async Task NotificationHub_InheritsConnectionRegistrationFromBaseHub()
    {
        var manager = new ConnectionManager();
        using var hub = CreateHub(manager, new RecordingHubClients(), new RecordingGroupManager());

        await hub.OnConnectedAsync();

        Assert.IsAssignableFrom<XiHanHub>(hub);
        Assert.Contains("caller-conn", await manager.GetConnectionsAsync("caller"));
    }

    /// <summary>
    /// 构造一个上下文齐备的通知 Hub
    /// </summary>
    /// <param name="connectionManager">连接管理器</param>
    /// <param name="clients">客户端集合替身</param>
    /// <param name="groups">组管理替身</param>
    /// <param name="userId">调用方用户 ID，传 null 表示匿名调用方</param>
    /// <returns></returns>
    private static NotificationHub CreateHub(
        IConnectionManager connectionManager,
        RecordingHubClients clients,
        RecordingGroupManager groups,
        string? userId = "caller")
    {
        return new NotificationHub(connectionManager)
        {
            Context = new FakeHubCallerContext(
                "caller-conn",
                userId is null ? TestPrincipals.Anonymous() : TestPrincipals.WithUserId(userId)),
            Clients = clients,
            Groups = groups
        };
    }
}
