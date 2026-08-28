// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Constants;
using XiHan.Framework.Web.RealTime.Services;
using XiHan.Framework.Web.RealTime.Tests.Infrastructure;

namespace XiHan.Framework.Web.RealTime.Tests.Services;

/// <summary>
/// 实时通知服务测试
/// </summary>
/// <remarks>
/// 该服务本身不发消息，它只做「用户 ID → 连接 ID」的翻译再转交 <c>IHubContext</c>。
/// 用例全部围绕这层编排：翻译是否正确、没有连接时是否短路、组操作是否覆盖用户全部连接。
/// </remarks>
public class RealtimeNotificationServiceTests
{
    /// <summary>
    /// 目标用户不在线时不发送任何消息
    /// </summary>
    [Fact]
    public async Task SendToUserAsync_WhenUserOffline_SendsNothing()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.SendToUserAsync("u1", SignalRConstants.ClientMethods.ReceiveNotification, "载荷");

        Assert.Empty(hubContext.ClientsRecorder.RequestedConnectionIdBatches);
        Assert.Empty(hubContext.ClientsRecorder.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 目标用户在线时向其全部连接发送
    /// </summary>
    [Fact]
    public async Task SendToUserAsync_WhenUserOnline_SendsToAllOfHisConnections()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");

        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, manager);

        await service.SendToUserAsync("u1", SignalRConstants.ClientMethods.ReceiveNotification, "载荷");

        Assert.Single(hubContext.ClientsRecorder.RequestedConnectionIdBatches);
        var addressed = hubContext.ClientsRecorder.RequestedConnectionIdBatches[0];
        Assert.Equal(2, addressed.Count);
        Assert.Contains("c1", addressed);
        Assert.Contains("c2", addressed);

        Assert.Single(hubContext.ClientsRecorder.ClientsProxy.Invocations);
        var invocation = hubContext.ClientsRecorder.ClientsProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.ReceiveNotification, invocation.Method);
        Assert.Single(invocation.Args);
        Assert.Equal("载荷", (string?)invocation.Args[0]);
    }

    /// <summary>
    /// 可变参数原样透传给客户端
    /// </summary>
    [Fact]
    public async Task SendToUserAsync_PassesEveryArgumentThrough()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, manager);

        await service.SendToUserAsync("u1", "任意方法", "第一个", 2, true);

        var invocation = hubContext.ClientsRecorder.ClientsProxy.Invocations[0];
        Assert.Equal(3, invocation.Args.Length);
        Assert.Equal("第一个", (string?)invocation.Args[0]);
        Assert.Equal(2, (int?)invocation.Args[1]);
        Assert.Equal(true, (bool?)invocation.Args[2]);
    }

    /// <summary>
    /// 多用户发送时把所有用户的连接合并成一次调用
    /// </summary>
    [Fact]
    public async Task SendToUsersAsync_MergesConnectionsOfEveryUserIntoOneCall()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u2", "c2");
        await manager.AddConnectionAsync("u2", "c3");

        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, manager);

        await service.SendToUsersAsync(["u1", "u2"], SignalRConstants.ClientMethods.ReceiveNotification, "载荷");

        Assert.Single(hubContext.ClientsRecorder.RequestedConnectionIdBatches);
        var addressed = hubContext.ClientsRecorder.RequestedConnectionIdBatches[0];
        Assert.Equal(3, addressed.Count);
        Assert.Contains("c1", addressed);
        Assert.Contains("c2", addressed);
        Assert.Contains("c3", addressed);
        Assert.Single(hubContext.ClientsRecorder.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 目标用户全部离线时不发送
    /// </summary>
    [Fact]
    public async Task SendToUsersAsync_WhenNobodyOnline_SendsNothing()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.SendToUsersAsync(["u1", "u2"], SignalRConstants.ClientMethods.ReceiveNotification, "载荷");

        Assert.Empty(hubContext.ClientsRecorder.RequestedConnectionIdBatches);
        Assert.Empty(hubContext.ClientsRecorder.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 用户列表为空时不发送
    /// </summary>
    [Fact]
    public async Task SendToUsersAsync_WithEmptyUserList_SendsNothing()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.SendToUsersAsync([], SignalRConstants.ClientMethods.ReceiveNotification, "载荷");

        Assert.Empty(hubContext.ClientsRecorder.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 全体广播不经过连接管理器，直接走 All 通道
    /// </summary>
    [Fact]
    public async Task SendToAllAsync_BroadcastsThroughAllChannel()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.SendToAllAsync(SignalRConstants.ClientMethods.ReceiveNotification, "公告");

        Assert.Single(hubContext.ClientsRecorder.AllProxy.Invocations);
        var invocation = hubContext.ClientsRecorder.AllProxy.Invocations[0];
        Assert.Equal(SignalRConstants.ClientMethods.ReceiveNotification, invocation.Method);
        Assert.Equal("公告", (string?)invocation.Args[0]);
        Assert.Empty(hubContext.ClientsRecorder.ClientsProxy.Invocations);
    }

    /// <summary>
    /// 组播寻址到指定组
    /// </summary>
    [Fact]
    public async Task SendToGroupAsync_SendsToNamedGroup()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.SendToGroupAsync(SignalRConstants.Groups.Admin, SignalRConstants.ClientMethods.ReceiveNotification, "运维通知");

        Assert.Single(hubContext.ClientsRecorder.RequestedGroupNames);
        Assert.Equal(SignalRConstants.Groups.Admin, hubContext.ClientsRecorder.RequestedGroupNames[0]);
        Assert.Single(hubContext.ClientsRecorder.GroupProxy.Invocations);
        Assert.Equal(
            SignalRConstants.ClientMethods.ReceiveNotification,
            hubContext.ClientsRecorder.GroupProxy.Invocations[0].Method);
    }

    /// <summary>
    /// 入组把用户名下每条连接都加进去
    /// </summary>
    [Fact]
    public async Task AddToGroupAsync_AddsEveryConnectionOfUser()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");

        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, manager);

        await service.AddToGroupAsync("u1", SignalRConstants.Groups.Notifications);

        Assert.Equal(2, hubContext.GroupsRecorder.Added.Count);
        Assert.All(hubContext.GroupsRecorder.Added, change =>
            Assert.Equal(SignalRConstants.Groups.Notifications, change.GroupName));
        Assert.Contains(hubContext.GroupsRecorder.Added, change => change.ConnectionId == "c1");
        Assert.Contains(hubContext.GroupsRecorder.Added, change => change.ConnectionId == "c2");
    }

    /// <summary>
    /// 用户不在线时入组是空操作
    /// </summary>
    [Fact]
    public async Task AddToGroupAsync_WhenUserOffline_AddsNothing()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.AddToGroupAsync("u1", SignalRConstants.Groups.Notifications);

        Assert.Empty(hubContext.GroupsRecorder.Added);
    }

    /// <summary>
    /// 出组把用户名下每条连接都移出去
    /// </summary>
    [Fact]
    public async Task RemoveFromGroupAsync_RemovesEveryConnectionOfUser()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");

        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, manager);

        await service.RemoveFromGroupAsync("u1", SignalRConstants.Groups.Users);

        Assert.Equal(2, hubContext.GroupsRecorder.Removed.Count);
        Assert.All(hubContext.GroupsRecorder.Removed, change =>
            Assert.Equal(SignalRConstants.Groups.Users, change.GroupName));
        Assert.Empty(hubContext.GroupsRecorder.Added);
    }

    /// <summary>
    /// 用户不在线时出组是空操作
    /// </summary>
    [Fact]
    public async Task RemoveFromGroupAsync_WhenUserOffline_RemovesNothing()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        await service.RemoveFromGroupAsync("u1", SignalRConstants.Groups.Users);

        Assert.Empty(hubContext.GroupsRecorder.Removed);
    }

    /// <summary>
    /// 实现满足对外暴露的实时通知契约
    /// </summary>
    [Fact]
    public void RealtimeNotificationService_ImplementsRealtimeNotificationContract()
    {
        var hubContext = new RecordingHubContext<TestXiHanHub>();
        var service = new RealtimeNotificationService<TestXiHanHub>(hubContext, new ConnectionManager());

        Assert.IsAssignableFrom<IRealtimeNotificationService<TestXiHanHub>>(service);
    }
}
