// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Auditing.InMemory;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.FileSystem;
using XiHan.Framework.Observability.Host;
using XiHan.Framework.Observability.OpenTelemetry;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.CloudEvents;
using XiHan.Framework.Protocols.Grpc;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Scheduling.InMemory;
using XiHan.Framework.Workflow.InMemory;

namespace XiHan.Framework.Profiles.Full;

/// <summary>
/// Full 组合。
/// </summary>
/// <remarks>
/// Standard + 对象存储、调度、审计的默认实现 + OpenTelemetry 导出与运行信息 + HTTP / gRPC / MCP / CloudEvents 协议适配器。远程配置与密钥没有默认实现（.NET 原生文件与环境变量），按需装配 Provider。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class FullProfile
{
    /// <summary>
    /// 装配 Full 组合。
    /// </summary>
    /// <remarks>
    /// 协议端点仍需在宿主上 <c>MapXiHanOperations()</c>、<c>MapXiHanGrpc()</c>、<c>MapXiHanMcp()</c>、<c>MapXiHanCloudEvents()</c> 映射。
    /// </remarks>
    public static AppBuilder UseFullProfile(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UseStandardProfile()
            .UseFeature<FileSystemObjectStorageFeature>()
            .UseFeature<InMemorySchedulingFeature>()
            .UseFeature<InMemoryAuditingFeature>()
            .UseFeature<InMemoryWorkflowFeature>()
            .UseFeature<AIToolsFeature>()
            .UseFeature<OpenTelemetryObservabilityFeature>()
            .UseFeature<HostObservabilityFeature>()
            .UseFeature<HttpProtocolFeature>()
            .UseFeature<GrpcProtocolFeature>()
            .UseFeature<McpProtocolFeature>()
            .UseFeature<CloudEventsProtocolFeature>();
    }
}
