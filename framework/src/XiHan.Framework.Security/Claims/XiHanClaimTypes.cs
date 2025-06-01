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
/// 曦寒身份声明类型
/// </summary>
/// <remarks>
/// 模仿者(Impersonator)，通常指的是在多租户(Multi-Tenant)系统中，进行身份冒充(Impersonation)操作时，用于标识发起冒充行为的租户ID。
/// 在一些系统中，用户(或管理员)可能会以其他用户的身份执行操作，这种行为称为“冒充”或“假冒”。
/// 为了保证安全性和审计的完整性，系统会记录下真正发起冒充操作的租户标识。
/// 这样可以区分操作的实际发起者和被冒充的用户，并帮助系统进行权限管理和日志记录。
/// </remarks>
public static class XiHanClaimTypes
{
    /// <summary>
    /// 用户名
    /// </summary>
    public static string UserName { get; set; } = ClaimTypes.Name;

    /// <summary>
    /// 名字
    /// </summary>
    public static string Name { get; set; } = ClaimTypes.GivenName;

    /// <summary>
    /// 姓氏
    /// </summary>
    public static string SurName { get; set; } = ClaimTypes.Surname;

    /// <summary>
    /// 用户标识
    /// </summary>
    public static string UserId { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// 角色
    /// </summary>
    public static string Role { get; set; } = ClaimTypes.Role;

    /// <summary>
    /// 邮箱
    /// </summary>
    public static string Email { get; set; } = ClaimTypes.Email;

    /// <summary>
    /// 邮箱是否已验证
    /// </summary>
    public static string EmailVerified { get; set; } = "email_verified";

    /// <summary>
    /// 手机号
    /// </summary>
    public static string PhoneNumber { get; set; } = "phone_number";

    /// <summary>
    /// 手机号是否已验证
    /// </summary>
    public static string PhoneNumberVerified { get; set; } = "phone_number_verified";

    /// <summary>
    /// 租户标识
    /// </summary>
    public static string TenantId { get; set; } = "tenantid";

    /// <summary>
    /// 版本标识
    /// </summary>
    public static string EditionId { get; set; } = "editionid";

    /// <summary>
    /// 客户端标识
    /// </summary>
    public static string ClientId { get; set; } = "client_id";

    /// <summary>
    /// 模仿者租户标识
    /// </summary>
    public static string ImpersonatorTenantId { get; set; } = "impersonator_tenantid";

    /// <summary>
    /// 模仿者用户标识
    /// </summary>
    public static string ImpersonatorUserId { get; set; } = "impersonator_userid";

    /// <summary>
    /// 模仿者租户名
    /// </summary>
    public static string ImpersonatorTenantName { get; set; } = "impersonator_tenantname";

    /// <summary>
    /// 模仿者用户名
    /// </summary>
    public static string ImpersonatorUserName { get; set; } = "impersonator_username";

    /// <summary>
    /// 头像路径
    /// </summary>
    public static string Picture { get; set; } = "picture";

    /// <summary>
    /// 是否记住
    /// </summary>
    public static string RememberMe { get; set; } = "remember_me";

    /// <summary>
    /// 会话标识
    /// </summary>
    public static string SessionId { get; set; } = "session_id";

    /// <summary>
    /// 设备指纹
    /// </summary>
    public static string DeviceFingerprint { get; set; } = "device_fingerprint";
}
