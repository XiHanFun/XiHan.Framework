// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.AspNetCore.Server.Model;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Protocols.Grpc.Methods;
using XiHan.Framework.Protocols.Grpc.Services;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// gRPC 协议适配器的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>protocols.grpc@1</c>，注册 gRPC 服务端、选项、消息目录、操作表与方法绑定；端点由应用在路由阶段调用 <c>MapXiHanGrpc()</c>（与 <c>MapXiHanProto()</c>）映射。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class GrpcProtocolFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.protocols.grpc",
        Package = "XiHan.Framework.Protocols.Grpc",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Protocol,
        Provides = [new CapabilityDeclaration(GrpcProtocolCapabilities.Grpc, GrpcProtocolCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 清单只声明领域级配置节 XiHan:Protocols；本适配器读其下的 Grpc 子节
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Protocols") },
        Entry = typeof(GrpcProtocolFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddGrpc();
        context.Services.AddMessageCatalog(GrpcMessages.Catalog);
        context.Services.AddSingleton(GrpcProtocolOptions.FromConfiguration(context.Configuration));
        // 操作表在首次解析时建成：那时全部特性已装配、登记表已定型
        context.Services.AddSingleton(static services => new GrpcOperationTable(services.GetRequiredService<OperationRegistry>(), services.GetRequiredService<GrpcProtocolOptions>()));
        context.Services.AddSingleton<IServiceMethodProvider<OperationsGrpcService>>(static services => new OperationMethodProvider(
            services.GetRequiredService<GrpcOperationTable>(),
            services.GetRequiredService<App>(),
            services.GetRequiredService<IOperationInvoker>(),
            services.GetService<IMessageLocalizer>()));
    }
}
