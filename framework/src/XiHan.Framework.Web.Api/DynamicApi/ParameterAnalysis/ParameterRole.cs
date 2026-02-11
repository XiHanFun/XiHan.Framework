#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterRole
// Guid:c69ca1b3-d1fe-44de-b5e0-ca363f900808
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数语义角色分类
/// </summary>
public enum ParameterRole
{
    /// <summary>
    /// 主键
    /// </summary>
    Id,

    /// <summary>
    /// 查询条件
    /// </summary>
    Query,

    /// <summary>
    /// 命令（Create / Update DTO）
    /// </summary>
    Command,

    /// <summary>
    /// 批量操作
    /// </summary>
    Batch,

    /// <summary>
    /// 基础设施参数
    /// </summary>
    Infra
}
