#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebAuthOptions
// Guid:b8c4d5e6-7f9a-4b1c-8d2e-3f4a5b6c7d8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
