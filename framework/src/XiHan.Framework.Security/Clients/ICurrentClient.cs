#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentClient
// Guid:da47aa01-6fec-49ef-a032-1a41896dd2fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 05:04:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Security.Clients;

/// <summary>
/// 当前客户端接口
/// </summary>
public interface ICurrentClient
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }
}
