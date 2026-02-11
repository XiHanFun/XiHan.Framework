#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthorizationContext
// Guid:c7d8e9f0-a1b2-3456-0123-123456789036
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 授权上下文
/// </summary>
public class AuthorizationContext
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户角色列表
    /// </summary>
    public List<string> UserRoles { get; set; } = [];

    /// <summary>
    /// 用户权限列表
    /// </summary>
    public List<string> UserPermissions { get; set; } = [];

    /// <summary>
    /// 用户声明字典
    /// </summary>
    public Dictionary<string, string> UserClaims { get; set; } = [];

    /// <summary>
    /// 资源对象
    /// </summary>
    public object? Resource { get; set; }

    /// <summary>
    /// 策略名称
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }
}
