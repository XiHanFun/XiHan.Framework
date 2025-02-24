#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanClaimTypes
// Guid:b8e149f4-b0b6-4917-b775-289c381818f8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 5:11:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;

namespace XiHan.Framework.Security.Claims;

/// <summary>
/// 曦寒声明类型
/// </summary>
public static class XiHanClaimTypes
{
    /// <summary>
    /// Default: <see cref="ClaimTypes.Name"/>
    /// </summary>
    public static string UserName { get; set; } = ClaimTypes.Name;

    /// <summary>
    /// Default: <see cref="ClaimTypes.GivenName"/>
    /// </summary>
    public static string Name { get; set; } = ClaimTypes.GivenName;

    /// <summary>
    /// Default: <see cref="ClaimTypes.Surname"/>
    /// </summary>
    public static string SurName { get; set; } = ClaimTypes.Surname;

    /// <summary>
    /// Default: <see cref="ClaimTypes.NameIdentifier"/>
    /// </summary>
    public static string UserId { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// Default: <see cref="ClaimTypes.Role"/>
    /// </summary>
    public static string Role { get; set; } = ClaimTypes.Role;

    /// <summary>
    /// Default: <see cref="ClaimTypes.Email"/>
    /// </summary>
    public static string Email { get; set; } = ClaimTypes.Email;

    /// <summary>
    /// Default: "email_verified".
    /// </summary>
    public static string EmailVerified { get; set; } = "email_verified";

    /// <summary>
    /// Default: "phone_number".
    /// </summary>
    public static string PhoneNumber { get; set; } = "phone_number";

    /// <summary>
    /// Default: "phone_number_verified".
    /// </summary>
    public static string PhoneNumberVerified { get; set; } = "phone_number_verified";

    /// <summary>
    /// Default: "tenantid".
    /// </summary>
    public static string TenantId { get; set; } = "tenantid";

    /// <summary>
    /// Default: "editionid".
    /// </summary>
    public static string EditionId { get; set; } = "editionid";

    /// <summary>
    /// Default: "client_id".
    /// </summary>
    public static string ClientId { get; set; } = "client_id";

    /// <summary>
    /// Default: "impersonator_tenantid".
    /// </summary>
    public static string ImpersonatorTenantId { get; set; } = "impersonator_tenantid";

    /// <summary>
    /// Default: "impersonator_userid".
    /// </summary>
    public static string ImpersonatorUserId { get; set; } = "impersonator_userid";

    /// <summary>
    /// Default: "impersonator_tenantname".
    /// </summary>
    public static string ImpersonatorTenantName { get; set; } = "impersonator_tenantname";

    /// <summary>
    /// Default: "impersonator_username".
    /// </summary>
    public static string ImpersonatorUserName { get; set; } = "impersonator_username";

    /// <summary>
    /// Default: "picture".
    /// </summary>
    public static string Picture { get; set; } = "picture";

    /// <summary>
    /// Default: "remember_me".
    /// </summary>
    public static string RememberMe { get; set; } = "remember_me";

    /// <summary>
    /// Default: "session_id".
    /// </summary>
    public static string SessionId { get; set; } = "session_id";
}
