// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace XiHan.Framework.Web.Mcp.Options;

/// <summary>
/// MCP 工具暴露配置器（按 <see cref="XiHanMcpOptions.AllowedTools"/> 与 <see cref="XiHanMcpOptions.DeniedTools"/> 裁剪经 /mcp 暴露的工具集）
/// </summary>
/// <remarks>
/// 被裁掉的工具既不出现在 tools/list，也不能经 tools/call 调用；两个清单都为空时不触碰工具集。
/// <para>
/// 裁剪的对象是 <see cref="McpServerOptions.ToolCollection"/>。宿主若另行设置
/// <c>Handlers.ListToolsHandler</c> / <c>CallToolHandler</c>，这两个 handler 提供的工具不在裁剪范围内；
/// 且 <c>CallToolHandler</c> 是「工具集里找不到才调用」的回退，被拒绝的名字有可能落到它身上。
/// 启用清单的宿主不应同时使用这两个 handler。
/// </para>
/// <para>
/// 清单含空白项时抛 <see cref="InvalidOperationException"/>。首次装配时核对清单项，
/// 在 <see cref="McpServerOptions.ToolCollection"/> 里匹配不到任何工具的清单项记一次警告。
/// </para>
/// </remarks>
public sealed class McpToolExposureConfigurator : IPostConfigureOptions<McpServerOptions>
{
    private readonly IOptions<XiHanMcpOptions> _options;
    private readonly ILogger<McpToolExposureConfigurator> _logger;

    /// <summary>
    /// 是否已核对过清单项（0 未核对，1 已核对）
    /// </summary>
    private int _entriesChecked;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">MCP 配置</param>
    /// <param name="logger">日志器</param>
    public McpToolExposureConfigurator(IOptions<XiHanMcpOptions> options, ILogger<McpToolExposureConfigurator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 按清单把不该暴露的工具从工具集里移除
    /// </summary>
    /// <param name="name">选项名（本包只用默认名，任何名字都按同一策略裁剪）</param>
    /// <param name="options">待裁剪的 MCP 服务端选项</param>
    /// <exception cref="InvalidOperationException">允许清单或拒绝清单含空白项</exception>
    public void PostConfigure(string? name, McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var policy = _options.Value;
        var allowed = ToNameSet(policy.AllowedTools, nameof(XiHanMcpOptions.AllowedTools));
        var denied = ToNameSet(policy.DeniedTools, nameof(XiHanMcpOptions.DeniedTools));

        // 两个清单都没配 = 不限制，保持既有暴露面
        if (allowed.Count == 0 && denied.Count == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _entriesChecked, 1) == 0)
        {
            ReportUnmatchedEntries(options.ToolCollection, allowed, denied);
        }

        if (options.ToolCollection is not { } tools)
        {
            return;
        }

        // 先取快照再删：边枚举边改集合不安全
        foreach (var tool in tools.ToArray())
        {
            if (!IsExposable(tool.ProtocolTool.Name, allowed, denied))
            {
                _ = tools.Remove(tool);
            }
        }
    }

    /// <summary>
    /// 判断一个工具名是否允许暴露
    /// </summary>
    /// <param name="toolName">工具名</param>
    /// <param name="allowed">允许清单，空集表示不限制</param>
    /// <param name="denied">拒绝清单</param>
    /// <returns>允许暴露时为 true</returns>
    private static bool IsExposable(string toolName, HashSet<string> allowed, HashSet<string> denied)
    {
        // 拒绝优先于允许：同时出现在两个清单里的名字必须消失
        if (denied.Contains(toolName))
        {
            return false;
        }

        return allowed.Count == 0 || allowed.Contains(toolName);
    }

    /// <summary>
    /// 把配置里的名字收成按序号比较的集合
    /// </summary>
    /// <param name="names">配置里的名字</param>
    /// <param name="listName">清单对应的选项属性名，用于异常信息</param>
    /// <returns>按序号比较的名字集合</returns>
    /// <exception cref="InvalidOperationException">清单含空白项</exception>
    private static HashSet<string> ToNameSet(List<string>? names, string listName)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (names is null)
        {
            return set;
        }

        for (var index = 0; index < names.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(names[index]))
            {
                throw new InvalidOperationException(
                    $"MCP 工具清单配置项 {XiHanMcpOptions.SectionName}:{listName}:{index} 为空白；请删除该项或填入工具名。");
            }

            _ = set.Add(names[index]);
        }

        return set;
    }

    /// <summary>
    /// 对在工具集里匹配不到任何工具的清单项记一次警告
    /// </summary>
    /// <param name="tools">裁剪前的工具集</param>
    /// <param name="allowed">允许清单</param>
    /// <param name="denied">拒绝清单</param>
    private void ReportUnmatchedEntries(McpServerPrimitiveCollection<McpServerTool>? tools, HashSet<string> allowed, HashSet<string> denied)
    {
        var toolNames = tools is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : tools.Select(tool => tool.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);

        var unmatchedAllowed = allowed.Where(entry => !toolNames.Contains(entry)).Order(StringComparer.Ordinal).ToArray();
        var unmatchedDenied = denied.Where(entry => !toolNames.Contains(entry)).Order(StringComparer.Ordinal).ToArray();

        if (unmatchedAllowed.Length == 0 && unmatchedDenied.Length == 0)
        {
            return;
        }

        _logger.LogWarning(
            "MCP 工具清单有项在工具集中匹配不到任何工具（名字区分大小写）：AllowedTools [{UnmatchedAllowedTools}]，DeniedTools [{UnmatchedDeniedTools}]。"
            + "拒绝清单写错名字不会屏蔽任何工具；若该工具由自定义 ListToolsHandler 提供，可忽略本警告。",
            string.Join(", ", unmatchedAllowed),
            string.Join(", ", unmatchedDenied));
    }
}
