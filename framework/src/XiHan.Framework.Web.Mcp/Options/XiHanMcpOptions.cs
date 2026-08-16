// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Mcp.Options;

/// <summary>
/// MCP Server 配置（appsettings 的 XiHan:AI:Mcp 节；属部署级基础设施配置）
/// </summary>
/// <remarks>
/// 鉴权为「应用管理的 key」：请求须带 <see cref="HeaderName"/>(或 Authorization: Bearer)且值等于 <see cref="ApiKey"/>。
/// fail-closed：未开启或未配置 ApiKey 则不暴露 /mcp 端点。
/// <para>
/// 端点一旦暴露，持有密钥者默认可调用宿主注册的**全部**技能；要把暴露面收窄到其中一部分，
/// 用 <see cref="AllowedTools"/> 与 <see cref="DeniedTools"/>。
/// </para>
/// </remarks>
public sealed class XiHanMcpOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:AI:Mcp";

    /// <summary>
    /// 是否启用 MCP Server（默认关；须显式开启并配置 ApiKey 才暴露端点）
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 应用管理的 MCP 访问密钥（外部 MCP 客户端须携带；空则不暴露端点）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 携带密钥的请求头名
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// 端点路径
    /// </summary>
    public string Path { get; set; } = "/mcp";

    /// <summary>
    /// 是否无状态 HTTP（无服务端→客户端回调；检索类工具足够，默认 true）
    /// </summary>
    public bool Stateless { get; set; } = true;

    /// <summary>
    /// 工具名允许清单（默认空表示不限制，即暴露全部工具；非空则只暴露清单内列出的工具）
    /// </summary>
    /// <remarks>
    /// 名字按序号（ordinal）比较且**区分大小写**，与 MCP 工具集自身按名索引的方式一致：大小写写错的名字
    /// 既不会放行也不会拦截，等同于没写。两个清单都为空时暴露面与不配置本项时逐字相同，既有宿主升级后行为不变。
    /// </remarks>
    public List<string> AllowedTools { get; set; } = [];

    /// <summary>
    /// 工具名拒绝清单（始终生效；同一个名字若同时出现在 <see cref="AllowedTools"/> 里，以拒绝为准）
    /// </summary>
    /// <remarks>
    /// 名字按序号（ordinal）比较且**区分大小写**，规则同 <see cref="AllowedTools"/>。
    /// 拒绝胜过允许是安全的方向：把名字写进拒绝清单的人期待它彻底消失，不论别处还说了什么。
    /// 被拒绝的工具既不出现在 tools/list，也不能经 tools/call 调用。
    /// </remarks>
    public List<string> DeniedTools { get; set; } = [];

    /// <summary>
    /// 是否已就绪暴露（启用 + 配了密钥）
    /// </summary>
    public bool IsExposable => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
