// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Auth;

/// <summary>
/// Web 认证授权配置选项
/// </summary>
public class XiHanWebAuthOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:Auth";

    /// <summary>
    /// 是否要求所有端点默认需要认证用户
    /// </summary>
    /// <remarks>
    /// 当为 true 时，所有未标记 [AllowAnonymous] 的端点都需要已认证用户。
    /// 仅在 JwtOptions.SecretKey 配置有效时生效
    /// </remarks>
    public bool RequireAuthenticatedUser { get; set; } = false;

    /// <summary>
    /// SignalR Hub 路径前缀，用于从 query string 提取 access_token
    /// </summary>
    public string SignalRHubPathPrefix { get; set; } = "/hubs";
}
