#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMultiTenantEntity
// Guid:2c4acb75-fb74-4e62-abdb-fab0956ad045
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:36:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 多租户实体接口
/// </summary>
public interface IMultiTenantEntity
{
    /// <summary>
    /// 租户ID
    /// </summary>
    long? TenantId { get; set; }
}
