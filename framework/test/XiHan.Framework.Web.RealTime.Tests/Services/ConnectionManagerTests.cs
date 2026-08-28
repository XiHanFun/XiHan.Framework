// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Services;

namespace XiHan.Framework.Web.RealTime.Tests.Services;

/// <summary>
/// 连接管理器测试
/// </summary>
/// <remarks>
/// 契约有三条：同一用户可以有多条连接；最后一条连接移除后用户即离线；所有方法在同一把锁下工作，
/// 允许被多个 Hub 连接线程并发调用。用例按这三条组织，不锁死内部使用的集合类型。
/// </remarks>
public class ConnectionManagerTests
{
    /// <summary>
    /// 新用户添加连接后即视为在线
    /// </summary>
    [Fact]
    public async Task AddConnectionAsync_ForNewUser_MakesUserOnline()
    {
        var manager = new ConnectionManager();

        await manager.AddConnectionAsync("u1", "c1");

        Assert.True(await manager.IsUserOnlineAsync("u1"));
        Assert.Equal(1, await manager.GetOnlineUserCountAsync());
        Assert.Contains("c1", await manager.GetConnectionsAsync("u1"));
    }

    /// <summary>
    /// 同一用户的多条连接全部保留
    /// </summary>
    [Fact]
    public async Task AddConnectionAsync_ForSameUserMultipleTimes_KeepsEveryConnection()
    {
        var manager = new ConnectionManager();

        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");
        await manager.AddConnectionAsync("u1", "c3");

        var connections = await manager.GetConnectionsAsync("u1");

        Assert.Equal(3, connections.Count);
        Assert.Contains("c1", connections);
        Assert.Contains("c2", connections);
        Assert.Contains("c3", connections);
        Assert.Equal(1, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 重复添加同一连接 ID 不会产生重复项
    /// </summary>
    [Fact]
    public async Task AddConnectionAsync_WithDuplicateConnectionId_IsIdempotent()
    {
        var manager = new ConnectionManager();

        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c1");

        var connections = await manager.GetConnectionsAsync("u1");

        Assert.Single(connections);
    }

    /// <summary>
    /// 不同用户之间互不干扰
    /// </summary>
    [Fact]
    public async Task AddConnectionAsync_ForDifferentUsers_KeepsConnectionsIsolated()
    {
        var manager = new ConnectionManager();

        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u2", "c2");

        var first = await manager.GetConnectionsAsync("u1");
        var second = await manager.GetConnectionsAsync("u2");

        Assert.Contains("c1", first);
        Assert.DoesNotContain("c2", first);
        Assert.Contains("c2", second);
        Assert.DoesNotContain("c1", second);
    }

    /// <summary>
    /// 用户还有其他连接时移除一条不会让其离线
    /// </summary>
    [Fact]
    public async Task RemoveConnectionAsync_WhenOtherConnectionsRemain_KeepsUserOnline()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");

        await manager.RemoveConnectionAsync("u1", "c1");

        var connections = await manager.GetConnectionsAsync("u1");

        Assert.True(await manager.IsUserOnlineAsync("u1"));
        Assert.Single(connections);
        Assert.Contains("c2", connections);
    }

    /// <summary>
    /// 移除最后一条连接后用户从在线集合中消失
    /// </summary>
    [Fact]
    public async Task RemoveConnectionAsync_WhenLastConnectionRemoved_MakesUserOffline()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        await manager.RemoveConnectionAsync("u1", "c1");

        Assert.False(await manager.IsUserOnlineAsync("u1"));
        Assert.Equal(0, await manager.GetOnlineUserCountAsync());
        Assert.Empty(await manager.GetOnlineUsersAsync());
        Assert.Empty(await manager.GetConnectionsAsync("u1"));
    }

    /// <summary>
    /// 移除不存在的用户是安全的空操作
    /// </summary>
    [Fact]
    public async Task RemoveConnectionAsync_ForUnknownUser_DoesNothing()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        await manager.RemoveConnectionAsync("不存在的用户", "c1");

        Assert.Equal(1, await manager.GetOnlineUserCountAsync());
        Assert.True(await manager.IsUserOnlineAsync("u1"));
    }

    /// <summary>
    /// 移除已知用户名下不存在的连接 ID 不影响其余连接
    /// </summary>
    [Fact]
    public async Task RemoveConnectionAsync_ForUnknownConnectionOfKnownUser_KeepsExistingConnections()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        await manager.RemoveConnectionAsync("u1", "c999");

        var connections = await manager.GetConnectionsAsync("u1");

        Assert.Single(connections);
        Assert.Contains("c1", connections);
    }

    /// <summary>
    /// 同一条连接重复移除不抛异常
    /// </summary>
    [Fact]
    public async Task RemoveConnectionAsync_CalledTwice_DoesNotThrow()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        await manager.RemoveConnectionAsync("u1", "c1");
        await manager.RemoveConnectionAsync("u1", "c1");

        Assert.False(await manager.IsUserOnlineAsync("u1"));
    }

    /// <summary>
    /// 未知用户的连接列表是空集合而不是 null
    /// </summary>
    [Fact]
    public async Task GetConnectionsAsync_ForUnknownUser_ReturnsEmptyList()
    {
        var manager = new ConnectionManager();

        var connections = await manager.GetConnectionsAsync("不存在的用户");

        Assert.NotNull(connections);
        Assert.Empty(connections);
    }

    /// <summary>
    /// 返回的连接列表是快照，后续增删不会回写到已取回的结果
    /// </summary>
    /// <remarks>
    /// 这是调用方能否安全遍历返回值的关键：内部集合非线程安全，只有快照语义才不会在遍历时被并发修改打断。
    /// </remarks>
    [Fact]
    public async Task GetConnectionsAsync_ReturnsSnapshotDetachedFromLaterChanges()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        var snapshot = await manager.GetConnectionsAsync("u1");
        await manager.AddConnectionAsync("u1", "c2");
        await manager.RemoveConnectionAsync("u1", "c1");

        Assert.Single(snapshot);
        Assert.Contains("c1", snapshot);
    }

    /// <summary>
    /// 在线用户列表包含每个还有连接的用户
    /// </summary>
    [Fact]
    public async Task GetOnlineUsersAsync_ReturnsEveryUserWithConnections()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u2", "c2");
        await manager.AddConnectionAsync("u2", "c3");

        var users = await manager.GetOnlineUsersAsync();

        Assert.Equal(2, users.Count);
        Assert.Contains("u1", users);
        Assert.Contains("u2", users);
    }

    /// <summary>
    /// 返回的在线用户列表是快照
    /// </summary>
    [Fact]
    public async Task GetOnlineUsersAsync_ReturnsSnapshotDetachedFromLaterChanges()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");

        var snapshot = await manager.GetOnlineUsersAsync();
        await manager.AddConnectionAsync("u2", "c2");

        Assert.Single(snapshot);
    }

    /// <summary>
    /// 在线用户数统计的是用户数而不是连接数
    /// </summary>
    [Fact]
    public async Task GetOnlineUserCountAsync_CountsUsersNotConnections()
    {
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("u1", "c1");
        await manager.AddConnectionAsync("u1", "c2");
        await manager.AddConnectionAsync("u1", "c3");
        await manager.AddConnectionAsync("u2", "c4");

        Assert.Equal(2, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 空管理器的在线用户数为零
    /// </summary>
    [Fact]
    public async Task GetOnlineUserCountAsync_WhenNobodyConnected_ReturnsZero()
    {
        var manager = new ConnectionManager();

        Assert.Equal(0, await manager.GetOnlineUserCountAsync());
        Assert.Empty(await manager.GetOnlineUsersAsync());
    }

    /// <summary>
    /// 未连接过的用户不在线
    /// </summary>
    [Fact]
    public async Task IsUserOnlineAsync_ForUnknownUser_ReturnsFalse()
    {
        var manager = new ConnectionManager();

        Assert.False(await manager.IsUserOnlineAsync("不存在的用户"));
    }

    /// <summary>
    /// 并发注册同一用户的大量连接不丢连接
    /// </summary>
    /// <remarks>
    /// 真实场景是同一账号多端同时握手；内部用的是非线程安全的 HashSet，靠外层锁保证不丢写。
    /// </remarks>
    [Fact]
    public async Task AddConnectionAsync_ConcurrentlyForSameUser_KeepsEveryConnection()
    {
        const int connectionCount = 300;
        var manager = new ConnectionManager();

        var tasks = Enumerable.Range(0, connectionCount)
            .Select(index => Task.Run(() => manager.AddConnectionAsync("u1", $"c{index}")))
            .ToArray();
        await Task.WhenAll(tasks);

        var connections = await manager.GetConnectionsAsync("u1");

        Assert.Equal(connectionCount, connections.Count);
        Assert.Equal(1, await manager.GetOnlineUserCountAsync());
    }

    /// <summary>
    /// 并发注册多个用户时用户数与每用户连接数都正确
    /// </summary>
    [Fact]
    public async Task AddConnectionAsync_ConcurrentlyForManyUsers_KeepsUserAndConnectionCounts()
    {
        const int userCount = 30;
        const int connectionsPerUser = 20;
        var manager = new ConnectionManager();

        var tasks = new List<Task>();
        for (var userIndex = 0; userIndex < userCount; userIndex++)
        {
            for (var connectionIndex = 0; connectionIndex < connectionsPerUser; connectionIndex++)
            {
                var userId = $"u{userIndex}";
                var connectionId = $"u{userIndex}-c{connectionIndex}";
                tasks.Add(Task.Run(() => manager.AddConnectionAsync(userId, connectionId)));
            }
        }

        await Task.WhenAll(tasks);

        Assert.Equal(userCount, await manager.GetOnlineUserCountAsync());
        for (var userIndex = 0; userIndex < userCount; userIndex++)
        {
            var connections = await manager.GetConnectionsAsync($"u{userIndex}");
            Assert.Equal(connectionsPerUser, connections.Count);
        }
    }

    /// <summary>
    /// 并发的「连上又断开」最终收敛为全部离线
    /// </summary>
    /// <remarks>
    /// 每个任务只加自己那条连接再移除自己那条，全部完成后加与减配平；
    /// 若移除时误删了别人的连接、或空集合没有及时从字典摘除，这条断言就会失败。
    /// </remarks>
    [Fact]
    public async Task AddAndRemoveConnectionAsync_Concurrently_ConvergesToOffline()
    {
        const int connectionCount = 200;
        var manager = new ConnectionManager();

        var tasks = Enumerable.Range(0, connectionCount)
            .Select(index => Task.Run(async () =>
            {
                await manager.AddConnectionAsync("u1", $"c{index}");
                await manager.RemoveConnectionAsync("u1", $"c{index}");
            }))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.False(await manager.IsUserOnlineAsync("u1"));
        Assert.Equal(0, await manager.GetOnlineUserCountAsync());
        Assert.Empty(await manager.GetConnectionsAsync("u1"));
    }

    /// <summary>
    /// 写入进行中并发读取不会抛出集合被修改异常
    /// </summary>
    [Fact]
    public async Task GetConnectionsAsync_WhileConcurrentWrites_NeverThrows()
    {
        const int iterationCount = 200;
        var manager = new ConnectionManager();

        var writer = Task.Run(async () =>
        {
            for (var index = 0; index < iterationCount; index++)
            {
                await manager.AddConnectionAsync("u1", $"c{index}");
                await manager.RemoveConnectionAsync("u1", $"c{index}");
            }
        });

        var reader = Task.Run(async () =>
        {
            for (var index = 0; index < iterationCount; index++)
            {
                foreach (var connectionId in await manager.GetConnectionsAsync("u1"))
                {
                    Assert.StartsWith("c", connectionId);
                }

                await manager.GetOnlineUsersAsync();
                await manager.GetOnlineUserCountAsync();
            }
        });

        await Task.WhenAll(writer, reader);

        Assert.False(await manager.IsUserOnlineAsync("u1"));
    }
}
