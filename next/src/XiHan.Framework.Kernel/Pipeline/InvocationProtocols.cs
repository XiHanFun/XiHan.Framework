// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// <see cref="InvocationOrigin.Protocol"/> 的取值：各协议适配器与派发器建上下文时写入哪一个（RFC-0003）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class InvocationProtocols
{
    /// <summary>
    /// HTTP 请求（含经 <c>UseXiHan()</c> 的 ASP.NET Core 端点）。
    /// </summary>
    public const string Http = "http";

    /// <summary>
    /// gRPC 一元调用。
    /// </summary>
    public const string Grpc = "grpc";

    /// <summary>
    /// MCP 的 <c>tools/call</c> 子调用（HTTP 或 stdio 传输）。
    /// </summary>
    public const string Mcp = "mcp";

    /// <summary>
    /// 命令行 <c>invoke</c>。
    /// </summary>
    public const string CommandLine = "cli";

    /// <summary>
    /// 消息派发（一次投递）。
    /// </summary>
    public const string Messaging = "messaging";

    /// <summary>
    /// 调度作业的一次运行。
    /// </summary>
    public const string Job = "job";

    /// <summary>
    /// 工作流活动的一次执行。
    /// </summary>
    public const string Workflow = "workflow";
}
