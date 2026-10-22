// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器的能力包入口：提供 <c>protocols.mcp@1</c>，注册选项、消息目录、<see cref="McpServer"/> 与命令行宿主的 <c>mcp</c> 命令（stdio 传输）；HTTP 端点由应用在路由阶段调用 <c>MapXiHanMcp()</c> 映射。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class McpProtocolFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.protocols.mcp",
        Package = "XiHan.Framework.Protocols.Mcp",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Protocol,
        Provides = [new CapabilityDeclaration(McpProtocolCapabilities.Mcp, McpProtocolCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 清单只声明领域级配置节 XiHan:Protocols；本适配器读其下的 Mcp 子节
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Protocols") },
        Entry = typeof(McpProtocolFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(McpMessages.Catalog);
        context.Services.AddSingleton(McpProtocolOptions.FromConfiguration(context.Configuration));
        // 命令行宿主存在时 `mcp` 命令以 stdio 提供同一个服务端；Web 宿主里这条注册没有消费者
        context.Services.AddXiHanCommand<McpStdioCommand>();
        // 服务端在首次解析时建 tool 表：那时全部特性已装配、登记表已定型；服务端名默认取宿主的应用名
        context.Services.AddSingleton(static services =>
        {
            var options = services.GetRequiredService<McpProtocolOptions>();
            var serverName = options.ServerName ?? services.GetService<IHostEnvironment>()?.ApplicationName ?? FrameworkInfo.Name;
            return new McpServer(
                services.GetRequiredService<OperationRegistry>(),
                services.GetRequiredService<IOperationInvoker>(),
                options,
                serverName,
                services.GetService<IMessageLocalizer>());
        });
    }
}
