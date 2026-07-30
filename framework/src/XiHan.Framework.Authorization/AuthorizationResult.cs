// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
