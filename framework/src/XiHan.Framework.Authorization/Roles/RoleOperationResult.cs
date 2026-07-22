// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 角色操作结果
/// </summary>
public class RoleOperationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 角色数据
    /// </summary>
    public RoleDefinition? Role { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static RoleOperationResult Success(RoleDefinition? role = null)
    {
        return new RoleOperationResult
        {
            Succeeded = true,
            Role = role
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static RoleOperationResult Failure(string errorMessage, string? errorCode = null)
    {
        return new RoleOperationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }
}
