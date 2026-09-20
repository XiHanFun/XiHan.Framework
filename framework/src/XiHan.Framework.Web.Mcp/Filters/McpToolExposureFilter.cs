// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Filters;

/// <summary>
/// MCP 工具暴露过滤器（按 <see cref="XiHanMcpOptions.AllowedTools"/> 与 <see cref="XiHanMcpOptions.DeniedTools"/> 裁剪经 /mcp 暴露的工具集）
/// </summary>
/// <remarks>
/// 被裁掉的工具既不出现在 tools/list，也不能经 tools/call 调用；两个清单都为空时不触碰工具集。
/// </remarks>
public sealed class McpToolExposureFilter : IPostConfigureOptions<McpServerOptions>
{
    private readonly IOptions<XiHanMcpOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public McpToolExposureFilter(IOptions<XiHanMcpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// 按清单把不该暴露的工具从工具集里移除
    /// </summary>
    /// <param name="name">选项名（本包只用默认名，任何名字都按同一策略裁剪）</param>
    /// <param name="options">待裁剪的 MCP 服务端选项</param>
    public void PostConfigure(string? name, McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var policy = _options.Value;
        var allowed = ToNameSet(policy.AllowedTools);
        var denied = ToNameSet(policy.DeniedTools);

        // 两个清单都没配 = 不限制，保持既有暴露面
        if (allowed.Count == 0 && denied.Count == 0)
        {
            return;
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
    /// 把配置里的名字收成按序号比较的集合（跳过空白项，配置里留空行不至于变成一个匹配不上的名字）
    /// </summary>
    /// <param name="names">配置里的名字</param>
    /// <returns>按序号比较的名字集合</returns>
    private static HashSet<string> ToNameSet(IEnumerable<string>? names)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (names is null)
        {
            return set;
        }

        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _ = set.Add(name);
            }
        }

        return set;
    }
}
