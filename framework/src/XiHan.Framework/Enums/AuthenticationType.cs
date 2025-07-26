#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthenticationType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 认证类型
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// JWT
    /// </summary>
    Jwt = 0,

    /// <summary>
    /// OAuth 2.0
    /// </summary>
    OAuth2 = 1,

    /// <summary>
    /// OpenID Connect
    /// </summary>
    OpenIdConnect = 2,

    /// <summary>
    /// Windows认证
    /// </summary>
    Windows = 3,

    /// <summary>
    /// 基本认证
    /// </summary>
    Basic = 4,

    /// <summary>
    /// 表单认证
    /// </summary>
    Forms = 5
}
