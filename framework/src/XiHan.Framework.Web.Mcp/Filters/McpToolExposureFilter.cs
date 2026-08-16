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
/// 为什么落在本包而不是技能投影处：允许/拒绝清单是 **HTTP 暴露面的部署级策略**，与 ApiKey 同层，属本包的事;
/// <c>XiHan.Framework.AI</c> 的技能投影与传输无关，不该认得本包的选项类型。
/// <para>
/// 为什么是 PostConfigure：它必在全部 <see cref="IConfigureOptions{TOptions}"/> 之后运行，
/// 于是官方 <c>AddMcpServer()</c> 收集的工具、本框架投影的技能工具都已就位，一次裁剪覆盖整个暴露面——
/// 只过滤技能的话，宿主经 <c>WithTools</c> 直接注册的工具会绕过清单。
/// </para>
/// <para>
/// 工具集同时供 tools/list 与 tools/call 使用，从中移除即两条路都断，不是只在列表里藏起来。
/// 两个清单都为空时直接返回、不触碰工具集，既有宿主升级后暴露面逐字不变。
/// </para>
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
