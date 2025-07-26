#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkSecurityAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架安全特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrameworkSecurityAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="securityLevel">安全级别</param>
    /// <param name="permissions">权限要求</param>
    /// <param name="roles">角色要求</param>
    public FrameworkSecurityAttribute(string securityLevel = "Normal", string[]? permissions = null, string[]? roles = null)
    {
        SecurityLevel = securityLevel;
        Permissions = permissions ?? [];
        Roles = roles ?? [];
    }

    /// <summary>
    /// 安全级别
    /// </summary>
    public string SecurityLevel { get; }

    /// <summary>
    /// 权限要求
    /// </summary>
    public string[] Permissions { get; }

    /// <summary>
    /// 角色要求
    /// </summary>
    public string[] Roles { get; }
}
