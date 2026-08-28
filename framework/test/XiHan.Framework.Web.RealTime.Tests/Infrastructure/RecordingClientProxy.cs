// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 记录客户端发送调用的代理替身
/// </summary>
/// <remarks>
/// 实现 <see cref="ISingleClientProxy"/> 而不是仅实现 <see cref="IClientProxy"/>，
/// 这样同一个类型既能当广播代理，也能当 Caller/Client 这类单连接代理，避免写两套替身。
/// </remarks>
public sealed class RecordingClientProxy : ISingleClientProxy
{
    private readonly ConcurrentQueue<ClientInvocation> _invocations = new();

    /// <summary>
    /// 已记录的发送调用（按发生顺序）
    /// </summary>
    public IReadOnlyList<ClientInvocation> Invocations => _invocations.ToArray();

    /// <summary>
    /// 记录一次发送调用
    /// </summary>
    /// <param name="method">客户端方法名</param>
    /// <param name="args">调用参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        _invocations.Enqueue(new ClientInvocation(method, args));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 带返回值的客户端调用不在本项目的契约范围内，被调用即说明用例写错了
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="method">客户端方法名</param>
    /// <param name="args">调用参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("测试替身不支持带返回值的客户端调用。");
    }
}
