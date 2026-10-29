// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 调用从哪里来。
/// </summary>
/// <remarks>
/// 协议、请求目标与传输层的客户端信息（RFC-0003）。由协议适配器在建上下文时写入一次，之后只读；不跨进程传播。访问日志、异常日志、登录日志与会话记录据此取远端地址与 User-Agent，与协议无关。内部发起的调用（缓存回源、存储维护等）没有来源。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InvocationOrigin
{
    /// <summary>
    /// 协议，取 <see cref="InvocationProtocols"/> 的值。
    /// </summary>
    public required string Protocol
    {
        get => field;
        init => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("The protocol must not be empty.", nameof(value)) : value;
    }

    /// <summary>
    /// 请求目标。
    /// </summary>
    /// <remarks>
    /// HTTP 为方法与路径（不含查询串），gRPC 为方法全名，MCP 为工具名，命令行为操作 ID，消息派发为主题，作业为作业名，工作流为活动类型。
    /// </remarks>
    public required string Target
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 远端地址。
    /// </summary>
    /// <remarks>
    /// IP 文本，不含端口；IPv4 映射的 IPv6 还原为 IPv4。经反向代理时取宿主转发头中间件改写后的值；没有网络对端（命令行、消息派发等）为 <see langword="null"/>。
    /// </remarks>
    public string? RemoteAddress { get; init; }

    /// <summary>
    /// 客户端的 User-Agent。
    /// </summary>
    /// <remarks>
    /// 没有时为 <see langword="null"/>。
    /// </remarks>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 来源页（HTTP <c>Referer</c>）。
    /// </summary>
    /// <remarks>
    /// 没有时为 <see langword="null"/>。
    /// </remarks>
    public string? Referer { get; init; }

    /// <summary>
    /// 宿主匹配到的端点的路由模板（如 <c>/api/files/{id}</c>）。
    /// </summary>
    /// <remarks>
    /// 按它聚合访问日志不受路径参数影响。没有匹配到路由端点（404、非路由的终结点）或协议没有端点时为 <see langword="null"/>。
    /// </remarks>
    public string? Route { get; init; }

    /// <summary>
    /// 宿主匹配到的端点的名字（ASP.NET Core 的 <c>WithName</c>）。
    /// </summary>
    /// <remarks>
    /// 端点没有命名或协议没有端点时为 <see langword="null"/>。普通端点（非操作端点）据此在 Observe 阶段被识别。
    /// </remarks>
    public string? EndpointName { get; init; }
}
