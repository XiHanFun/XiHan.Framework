// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Web.Options;

/// <summary>
/// 文档 MCP Server 的 HTTP 传输配置（appsettings 的 XiHan:Docs:Mcp 节；属部署级基础设施配置）
/// </summary>
/// <remarks>
/// 鉴权为「部署方管理的 key」：请求须带 <see cref="HeaderName"/>(或 Authorization: Bearer)且值等于 <see cref="ApiKey"/>。
/// fail-closed：未开启或未配置 ApiKey 则既不注册 MCP 服务也不映射 /mcp 端点——半配好的部署暴露不出任何东西，
/// 而不是暴露一个不设防的端点。
/// <para>
/// 与 <c>XiHan.Framework.Web.Mcp</c> 的 <c>XiHanMcpOptions</c> 字段一一对应，只有配置节名不同：
/// 那个节（<c>XiHan:AI:Mcp</c>）属于宿主应用，这个节属于本文档服务端，两者可以在同一台机器上各配各的。
/// </para>
/// </remarks>
public sealed class XiHanDocsMcpWebOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:Docs:Mcp";

    /// <summary>
    /// 是否启用 MCP Server（默认关；须显式开启并配置 ApiKey 才暴露端点）
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 部署方管理的 MCP 访问密钥（外部 MCP 客户端须携带；空则不暴露端点）
    /// </summary>
    /// <remarks>
    /// 绝不写进仓库：开发期用 <c>dotnet user-secrets</c>，部署期用环境变量 <c>XiHan__Docs__Mcp__ApiKey</c>。
    /// </remarks>
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
    /// 是否无状态 HTTP（无服务端→客户端回调；三个工具都是纯检索，默认 true）
    /// </summary>
    public bool Stateless { get; set; } = true;

    /// <summary>
    /// 是否已就绪暴露（启用 + 配了密钥）
    /// </summary>
    public bool IsExposable => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
