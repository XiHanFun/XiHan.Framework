#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMultiTenant
// Guid:3456769a-f8c3-4e9b-a709-cf3d7744e07c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 8:17:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户实体接口
/// 定义支持多租户架构的实体契约，用于标识实体所属的租户
/// 实现此接口的实体将自动支持租户隔离和数据过滤功能
/// </summary>
public interface IMultiTenant
{
    /// <summary>
    /// 获取关联租户的唯一标识符
    /// 用于标识该实体所属的租户，实现数据隔离
    /// </summary>
    /// <value>
    /// 租户的全局唯一 Id，如果为 null 则表示该实体属于宿主（Host）或公共数据
    /// </value>
    /// <remarks>
    /// 在多租户系统中，此属性用于：
    /// <list type="bullet">
    /// <item>数据查询时的自动租户过滤</item>
    /// <item>确保用户只能访问自己租户的数据</item>
    /// <item>在数据保存时自动设置租户标识</item>
    /// </list>
    /// </remarks>
    Guid? TenantId { get; }
}
