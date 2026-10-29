// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Tools;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Protocols:Mcp</c> 显式读取：<c>ServerName</c>、<c>ServerVersion</c>、<c>Instructions</c>，<c>Tools:&lt;操作 ID&gt;</c> = tool 名显式覆盖推导的名字。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpProtocolOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:Mcp";

    /// <summary>
    /// <c>initialize</c> 答复里的服务端名。
    /// </summary>
    /// <remarks>
    /// 空表示用应用名。
    /// </remarks>
    public string? ServerName { get; init; }

    /// <summary>
    /// <c>initialize</c> 答复里的服务端版本。
    /// </summary>
    /// <remarks>
    /// 默认为框架版本。
    /// </remarks>
    public string ServerVersion
    {
        get => field;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = FrameworkInfo.Version;

    /// <summary>
    /// <c>initialize</c> 答复里给客户端模型的使用说明。
    /// </summary>
    /// <remarks>
    /// 空表示不给。
    /// </remarks>
    public string? Instructions { get; init; }

    /// <summary>
    /// 显式覆盖。
    /// </summary>
    /// <remarks>
    /// 操作 ID → tool 名；未覆盖的操作按 <see cref="McpToolNameConvention"/> 推导。
    /// </remarks>
    public IReadOnlyDictionary<string, string> Tools
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (id, name) in value)
            {
                OperationId.Validate(id);
                McpToolName.Validate(name);
            }

            field = value;
        }
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static McpProtocolOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var tools = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Tools").GetChildren())
        {
            if (child.Value is { } name)
            {
                tools[child.Key] = name;
            }
        }

        return new McpProtocolOptions
        {
            ServerName = section["ServerName"],
            ServerVersion = section["ServerVersion"] ?? FrameworkInfo.Version,
            Instructions = section["Instructions"],
            Tools = tools,
        };
    }

    /// <summary>
    /// 操作最终的 tool 名：显式覆盖优先，否则按约定推导。
    /// </summary>
    public string ToolNameFor(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Tools.TryGetValue(contract.Id, out var overridden) ? overridden : McpToolNameConvention.Derive(contract);
    }
}
