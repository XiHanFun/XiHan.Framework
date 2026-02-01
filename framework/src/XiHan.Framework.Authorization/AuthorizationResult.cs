#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthorizationResult
// Guid:a1b2c3d4-e5f6-7890-4567-123456789040
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization;

/// <summary>
/// 授权结果
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// 是否授权成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 失败的要求列表
    /// </summary>
    public List<string> FailedRequirements { get; set; } = [];

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static AuthorizationResult Success()
    {
        return new AuthorizationResult
        {
            Succeeded = true
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static AuthorizationResult Failure(string failureReason, List<string>? failedRequirements = null)
    {
        return new AuthorizationResult
        {
            Succeeded = false,
            FailureReason = failureReason,
            FailedRequirements = failedRequirements ?? []
        };
    }

    /// <summary>
    /// 创建权限不足的结果
    /// </summary>
    public static AuthorizationResult PermissionDenied(string permissionName)
    {
        return new AuthorizationResult
        {
            Succeeded = false,
            FailureReason = $"缺少权限: {permissionName}",
            FailedRequirements = [permissionName]
        };
    }

    /// <summary>
    /// 创建角色不足的结果
    /// </summary>
    public static AuthorizationResult RoleDenied(string roleName)
    {
        return new AuthorizationResult
        {
            Succeeded = false,
            FailureReason = $"不在角色中: {roleName}",
            FailedRequirements = [roleName]
        };
    }
}
