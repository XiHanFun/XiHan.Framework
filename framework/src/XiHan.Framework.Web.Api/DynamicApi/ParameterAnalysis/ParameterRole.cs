// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数语义角色分类
/// </summary>
/// <remarks>
/// 仅为描述性元数据，不参与绑定位置决策。绑定位置只由显式绑定特性与 HTTP 方法、参数类型决定。
/// </remarks>
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
