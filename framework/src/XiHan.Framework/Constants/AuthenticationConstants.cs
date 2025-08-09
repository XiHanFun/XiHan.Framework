#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthenticationConstants
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5c8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Constants;

/// <summary>
/// 认证授权相关常量
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// 默认CORS策略名称
    /// </summary>
    public const string DefaultCorsPolicyName = "DefaultCorsPolicy";

    /// <summary>
    /// 默认认证方案名称
    /// </summary>
    public const string DefaultAuthenticationScheme = "Bearer";

    /// <summary>
    /// 默认授权策略名称
    /// </summary>
    public const string DefaultAuthorizationPolicy = "DefaultPolicy";

    /// <summary>
    /// 默认角色名称
    /// </summary>
    public const string DefaultRoleName = "User";

    /// <summary>
    /// 超级管理员角色名称
    /// </summary>
    public const string SuperAdminRoleName = "SuperAdmin";

    /// <summary>
    /// 管理员角色名称
    /// </summary>
    public const string AdminRoleName = "Admin";

    /// <summary>
    /// 系统角色名称
    /// </summary>
    public const string SystemRoleName = "System";
}
