// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Hubs;
using XiHan.Framework.Web.RealTime.Services;
using XiHan.Framework.Web.RealTime.Tests.Infrastructure;

namespace XiHan.Framework.Web.RealTime.Tests.Hubs;

/// <summary>
/// 曦寒 Hub 基类测试
/// </summary>
/// <remarks>
/// 基类只做两件事：从 <c>Context</c> 的声明里解析身份，以及在连接生命周期两端维护连接管理器。
/// 用例直接 new 具体子类并手工赋 <c>Context</c>，不起真实 SignalR 服务器。
/// </remarks>
public class XiHanHubTests
{
    /// <summary>
    /// 连接建立时按用户 ID 登记连接
    /// </summary>
    [Fact]
    public async Task OnConnectedAsync_WithAuthenticatedUser_RegistersConnection()
    {
        var manager = new ConnectionManager();
        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserId("u1"))
        };

        await hub.OnConnectedAsync();

        var connections = await manager.GetConnectionsAsync("u1");
        Assert.Single(connections);
        Assert.Contains("conn-1", connections);
    }

    /// <summary>
    /// 同一用户两条连接分别登记后都在线
    /// </summary>
    [Fact]
    public async Task OnConnectedAsync_ForSameUserTwice_KeepsBothConnections()
    {
        var manager = new ConnectionManager();
        var principal = TestPrincipals.WithUserId("u1");

        using (var first = new TestXiHanHub(manager) { Context = new FakeHubCallerContext("conn-1", principal) })
        {
            await first.OnConnectedAsync();
        }

        using (var second = new TestXiHanHub(manager) { Context = new FakeHubCallerContext("conn-2", principal) })
        {
            await second.OnConnectedAsync();
        }

        var connections = await manager.GetConnectionsAsync("u1");
        Assert.Equal(2, connections.Count);
        Assert.Equal(1, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 匿名连接不登记任何连接
    /// </summary>
    [Fact]
    public async Task OnConnectedAsync_WhenUserIdClaimMissing_RegistersNothing()
    {
        var manager = new ConnectionManager();
        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.Anonymous())
        };

        await hub.OnConnectedAsync();

        Assert.Equal(0, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 没有用户主体时也不登记
    /// </summary>
    [Fact]
    public async Task OnConnectedAsync_WhenUserAbsent_RegistersNothing()
    {
        var manager = new ConnectionManager();
        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1")
        };

        await hub.OnConnectedAsync();

        Assert.Equal(0, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 断开连接只注销自己这一条连接
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_RemovesOnlyOwnConnection()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "conn-1");
        await manager.AddConnectionAsync("u1", "conn-2");

        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserId("u1"))
        };

        await hub.OnDisconnectedAsync(null);

        var connections = await manager.GetConnectionsAsync("u1");
        Assert.Single(connections);
        Assert.Contains("conn-2", connections);
    }

    /// <summary>
    /// 带异常断开时同样完成注销
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_WithException_StillRemovesConnection()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "conn-1");

        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserId("u1"))
        };

        await hub.OnDisconnectedAsync(new IOException("网络中断"));

        Assert.False(await manager.IsUserOnlineAsync("u1"));
    }

    /// <summary>
    /// 匿名连接断开时不动已有连接
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_WhenUserIdClaimMissing_LeavesManagerUntouched()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "conn-1");

        using var hub = new TestXiHanHub(manager)
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.Anonymous())
        };

        await hub.OnDisconnectedAsync(null);

        Assert.True(await manager.IsUserOnlineAsync("u1"));
        Assert.Single(await manager.GetConnectionsAsync("u1"));
    }

    /// <summary>
    /// 连接 ID 来自 Hub 调用上下文
    /// </summary>
    [Fact]
    public void ConnectionId_ComesFromHubCallerContext()
    {
        using var hub = new TestXiHanHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-42")
        };

        Assert.Equal("conn-42", hub.ConnectionId);
    }

    /// <summary>
    /// 用户 ID 取 NameIdentifier 声明
    /// </summary>
    [Fact]
    public void UserId_ReadsNameIdentifierClaim()
    {
        using var hub = new TestXiHanHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserIdAndName("u1", "张三"))
        };

        Assert.Equal("u1", hub.UserId);
    }

    /// <summary>
    /// 用户名取 Name 声明
    /// </summary>
    [Fact]
    public void UserName_ReadsNameClaim()
    {
        using var hub = new TestXiHanHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserIdAndName("u1", "张三"))
        };

        Assert.Equal("张三", hub.UserName);
    }

    /// <summary>
    /// 只有用户名声明时用户 ID 为 null，不会回退到用户名
    /// </summary>
    /// <remarks>
    /// 与 <c>XiHanUserIdProvider</c> 的回退策略不同：Hub 基类不做回退。两处口径差异是刻意固定下来的。
    /// </remarks>
    [Fact]
    public void UserId_WhenOnlyNameClaimPresent_IsNull()
    {
        using var hub = new TestXiHanHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserName("张三"))
        };

        Assert.Null(hub.UserId);
        Assert.Equal("张三", hub.UserName);
    }

    /// <summary>
    /// 匿名连接的身份字段全部为 null
    /// </summary>
    [Fact]
    public void UserIdAndUserName_WhenUserAbsent_AreNull()
    {
        using var hub = new TestXiHanHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-1")
        };

        Assert.Null(hub.UserId);
        Assert.Null(hub.UserName);
    }

    /// <summary>
    /// Hub 基类对外暴露的是曦寒 Hub 契约
    /// </summary>
    [Fact]
    public void XiHanHub_ImplementsXiHanHubContract()
    {
        using var hub = new TestXiHanHub(new ConnectionManager());

        Assert.IsAssignableFrom<IXiHanHub>(hub);
        Assert.True(typeof(XiHanHub).IsAbstract);
    }
}
