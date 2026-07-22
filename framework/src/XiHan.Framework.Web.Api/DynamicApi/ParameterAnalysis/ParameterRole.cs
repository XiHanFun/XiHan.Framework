// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
