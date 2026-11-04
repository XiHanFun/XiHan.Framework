// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 登记知识库三个只读操作的特性
/// </summary>
/// <remarks>
/// 只在 <c>xihan mcp</c> 启动的内嵌应用里装配：三个操作只暴露给 MCP，tool 名由 <c>XiHan:Protocols:Mcp:Tools</c> 改写成 4.x 沿用的 <c>xihan_framework_*</c>；标题与描述在本工具的消息目录里（<c>cli.info.title</c> 等）
/// </remarks>
internal sealed class KnowledgeFeature : IFeature
{
    /// <summary>
    /// MCP 服务端名，也是编码助手配置里的服务名
    /// </summary>
    public const string ServerName = "xihan-framework";

    /// <summary>
    /// 查包、特性与能力的操作 ID
    /// </summary>
    public const string InfoOperationId = "cli.info";

    /// <summary>
    /// 查配置节的操作 ID
    /// </summary>
    public const string ConfigOperationId = "cli.config";

    /// <summary>
    /// 检索文档的操作 ID
    /// </summary>
    public const string SearchOperationId = "cli.search";

    /// <summary>
    /// 操作 ID → 发布的 tool 名
    /// </summary>
    public static IReadOnlyList<(string Operation, string Tool)> Tools { get; } =
    [
        (InfoOperationId, "xihan_framework_info"),
        (ConfigOperationId, "xihan_framework_config"),
        (SearchOperationId, "xihan_framework_search"),
    ];

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.cli.knowledge",
        Package = "XiHan.Framework.Cli",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Operations = [InfoOperationId, ConfigOperationId, SearchOperationId] },
        Entry = typeof(KnowledgeFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(CliMessages.Catalog);
        // 三个操作都只读、只给 MCP：编码助手据只读提示决定无需确认即可调用
        var json = KnowledgeJsonContext.Relaxed;
        context.AddOperation<InfoOperation, InfoRequest, InfoResponse>(Contract(InfoOperationId), json.InfoRequest, json.InfoResponse, KnowledgeSchemas.Info);
        context.AddOperation<ConfigOperation, ConfigRequest, ConfigResponse>(Contract(ConfigOperationId), json.ConfigRequest, json.ConfigResponse, KnowledgeSchemas.Config);
        context.AddOperation<SearchOperation, SearchRequest, SearchResponse>(Contract(SearchOperationId), json.SearchRequest, json.SearchResponse, KnowledgeSchemas.Search);
    }

    /// <summary>
    /// 只读、只暴露给 MCP 的契约
    /// </summary>
    private static OperationContract Contract(string id) => new(id, Stability.Preview, Expose.Mcp, readOnly: true);
}
