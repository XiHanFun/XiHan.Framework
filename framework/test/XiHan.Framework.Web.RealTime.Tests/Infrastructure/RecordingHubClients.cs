// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 记录客户端寻址与发送的客户端集合替身
/// </summary>
/// <remarks>
/// 同时实现 <see cref="IHubClients"/>（<c>IHubContext</c> 用）与 <see cref="IHubCallerClients"/>（<c>Hub.Clients</c> 用），
/// 一个替身覆盖两条调用链。
/// 每种寻址方式返回各自独立的代理实例，用例据此区分「发给谁」而不只是「发了什么」。
/// </remarks>
public sealed class RecordingHubClients : IHubClients, IHubCallerClients
{
    /// <summary>
    /// 全体广播代理
    /// </summary>
    public RecordingClientProxy AllProxy { get; } = new();

    /// <summary>
    /// 排除部分连接的广播代理
    /// </summary>
    public RecordingClientProxy AllExceptProxy { get; } = new();

    /// <summary>
    /// 调用方代理
    /// </summary>
    public RecordingClientProxy CallerProxy { get; } = new();

    /// <summary>
    /// 单连接代理
    /// </summary>
    public RecordingClientProxy SingleClientProxy { get; } = new();

    /// <summary>
    /// 指定连接集合代理
    /// </summary>
    public RecordingClientProxy ClientsProxy { get; } = new();

    /// <summary>
    /// 单个组代理
    /// </summary>
    public RecordingClientProxy GroupProxy { get; } = new();

    /// <summary>
    /// 排除部分连接的组代理
    /// </summary>
    public RecordingClientProxy GroupExceptProxy { get; } = new();

    /// <summary>
    /// 多个组代理
    /// </summary>
    public RecordingClientProxy GroupsProxy { get; } = new();

    /// <summary>
    /// 除调用方之外的代理
    /// </summary>
    public RecordingClientProxy OthersProxy { get; } = new();

    /// <summary>
    /// 组内除调用方之外的代理
    /// </summary>
    public RecordingClientProxy OthersInGroupProxy { get; } = new();

    /// <summary>
    /// 单个用户代理
    /// </summary>
    public RecordingClientProxy UserProxy { get; } = new();

    /// <summary>
    /// 多个用户代理
    /// </summary>
    public RecordingClientProxy UsersProxy { get; } = new();

    /// <summary>
    /// 每次 <c>Clients(ids)</c> 请求到的连接集合
    /// </summary>
    public List<IReadOnlyList<string>> RequestedConnectionIdBatches { get; } = [];

    /// <summary>
    /// 每次 <c>Group(name)</c> 请求到的组名
    /// </summary>
    public List<string> RequestedGroupNames { get; } = [];

    /// <summary>
    /// 每次 <c>Client(id)</c> 请求到的连接 ID
    /// </summary>
    public List<string> RequestedSingleConnectionIds { get; } = [];

    /// <summary>
    /// 每次 <c>User(id)</c> 请求到的用户 ID
    /// </summary>
    public List<string> RequestedUserIds { get; } = [];

    /// <summary>
    /// 全体客户端
    /// </summary>
    public IClientProxy All => AllProxy;

    /// <summary>
    /// 除调用方之外的客户端
    /// </summary>
    public IClientProxy Others => OthersProxy;

    /// <summary>
    /// 调用方客户端
    /// </summary>
    public ISingleClientProxy Caller => CallerProxy;

    /// <summary>
    /// 泛型接口上的调用方客户端
    /// </summary>
    IClientProxy IHubCallerClients<IClientProxy>.Caller => CallerProxy;

    /// <summary>
    /// 排除部分连接的全体客户端
    /// </summary>
    /// <param name="excludedConnectionIds">排除的连接 ID</param>
    /// <returns></returns>
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
    {
        return AllExceptProxy;
    }

    /// <summary>
    /// 指定单个连接
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    public ISingleClientProxy Client(string connectionId)
    {
        RequestedSingleConnectionIds.Add(connectionId);
        return SingleClientProxy;
    }

    /// <summary>
    /// 泛型接口上的单连接寻址
    /// </summary>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    IClientProxy IHubClients<IClientProxy>.Client(string connectionId)
    {
        return Client(connectionId);
    }

    /// <summary>
    /// 指定一批连接
    /// </summary>
    /// <param name="connectionIds">连接 ID 集合</param>
    /// <returns></returns>
    public IClientProxy Clients(IReadOnlyList<string> connectionIds)
    {
        RequestedConnectionIdBatches.Add(connectionIds);
        return ClientsProxy;
    }

    /// <summary>
    /// 指定单个组
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public IClientProxy Group(string groupName)
    {
        RequestedGroupNames.Add(groupName);
        return GroupProxy;
    }

    /// <summary>
    /// 指定组并排除部分连接
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <param name="excludedConnectionIds">排除的连接 ID</param>
    /// <returns></returns>
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
    {
        return GroupExceptProxy;
    }

    /// <summary>
    /// 指定多个组
    /// </summary>
    /// <param name="groupNames">组名集合</param>
    /// <returns></returns>
    public IClientProxy Groups(IReadOnlyList<string> groupNames)
    {
        return GroupsProxy;
    }

    /// <summary>
    /// 指定组内除调用方之外的连接
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public IClientProxy OthersInGroup(string groupName)
    {
        return OthersInGroupProxy;
    }

    /// <summary>
    /// 指定单个用户
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    public IClientProxy User(string userId)
    {
        RequestedUserIds.Add(userId);
        return UserProxy;
    }

    /// <summary>
    /// 指定多个用户
    /// </summary>
    /// <param name="userIds">用户 ID 集合</param>
    /// <returns></returns>
    public IClientProxy Users(IReadOnlyList<string> userIds)
    {
        return UsersProxy;
    }
}
