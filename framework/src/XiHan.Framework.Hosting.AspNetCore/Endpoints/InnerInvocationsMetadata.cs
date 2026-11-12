// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 端点元数据：端点为请求里的消息另起内层调用
/// </summary>
/// <remarks>
/// 外层请求照常经内核管道（认证、来源），但不按幂等键去重：幂等键交给端点以 <see cref="HttpInvocation.CreateContext(Microsoft.AspNetCore.Http.HttpContext, IServiceProvider)"/> 另起的内层调用（如 MCP 的 <c>tools/call</c>）。外层是普通端点，回放只能还原结果、还原不了答复正文；外层先写下的「处理中」还会挡住同一个键的内层调用（ADR-0205）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InnerInvocationsMetadata
{
    /// <summary>
    /// 不允许外部构造
    /// </summary>
    private InnerInvocationsMetadata()
    {
    }

    /// <summary>
    /// 共享实例
    /// </summary>
    public static InnerInvocationsMetadata Instance { get; } = new();

    /// <summary>
    /// 标注端点为请求里的消息另起内层调用
    /// </summary>
    public static TBuilder HandsIdempotencyToInnerInvocations<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(Instance);
        return builder;
    }
}
