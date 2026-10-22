// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// 映射 gRPC 端点：操作表里的每个操作一个一元方法（<c>/{package}.{Service}/{Method}</c>），以及发布生成的 <c>.proto</c> 文本的端点。需要 HTTP/2 的监听端点（明文 h2c 时 Kestrel 该端点的 <c>Protocols</c> 须为 <c>Http2</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class OperationGrpcEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射全部暴露给 gRPC 的操作；需要 <see cref="GrpcProtocolFeature"/> 已装配。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanGrpc(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = endpoints.ServiceProvider.GetService<GrpcOperationTable>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanGrpc() requires the gRPC protocol feature; add UseFeature<GrpcProtocolFeature>() to the application.");

        return endpoints.MapGrpcService<OperationsGrpcService>();
    }

    /// <summary>
    /// 映射 <c>.proto</c> 文本端点（<c>text/plain</c>），不经过内核管道；供客户端用 protoc / grpcurl 生成或调用。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanProto(this IEndpointRouteBuilder endpoints, string pattern = "/operations.proto")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var table = endpoints.ServiceProvider.GetService<GrpcOperationTable>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanProto() requires the gRPC protocol feature; add UseFeature<GrpcProtocolFeature>() to the application.");
        var proto = Encoding.UTF8.GetBytes(table.Proto);

        return PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(pattern, async httpContext =>
        {
            httpContext.Response.ContentType = "text/plain; charset=utf-8";
            await httpContext.Response.Body.WriteAsync(proto, httpContext.RequestAborted).ConfigureAwait(false);
        }));
    }
}
