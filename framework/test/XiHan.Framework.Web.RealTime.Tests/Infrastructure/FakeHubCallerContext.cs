// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// Hub 调用方上下文的最小替身
/// </summary>
/// <remarks>
/// SignalR 的真实实现是 internal，测试只能自己继承抽象基类；这里只提供 Hub 与过滤器真正读到的成员。
/// </remarks>
public sealed class FakeHubCallerContext : HubCallerContext
{
    private readonly CancellationTokenSource _abortSource = new();
    private readonly string _connectionId;
    private readonly ClaimsPrincipal? _user;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <param name="user">当前用户主体，传 null 表示匿名连接</param>
    public FakeHubCallerContext(string connectionId = "conn-1", ClaimsPrincipal? user = null)
    {
        _connectionId = connectionId;
        _user = user;
    }

    /// <summary>
    /// 是否被中止过
    /// </summary>
    public bool Aborted { get; private set; }

    /// <summary>
    /// 连接 ID
    /// </summary>
    public override string ConnectionId => _connectionId;

    /// <summary>
    /// 用户标识
    /// </summary>
    public override string? UserIdentifier => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// 当前用户主体
    /// </summary>
    public override ClaimsPrincipal? User => _user;

    /// <summary>
    /// 连接范围内的键值集合
    /// </summary>
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    /// <summary>
    /// 连接特性集合
    /// </summary>
    public override IFeatureCollection Features { get; } = new FeatureCollection();

    /// <summary>
    /// 连接中止令牌
    /// </summary>
    public override CancellationToken ConnectionAborted => _abortSource.Token;

    /// <summary>
    /// 中止连接
    /// </summary>
    public override void Abort()
    {
        Aborted = true;
        _abortSource.Cancel();
    }
}
