// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Attributes;

/// <summary>
/// 审计特性，用于标记实体是否启用各类审计功能
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AuditableAttribute : Attribute
{
    /// <summary>
    /// 是否启用创建审计（记录创建时间、创建人）
    /// </summary>
    public bool EnableCreationAudit { get; set; } = true;

    /// <summary>
    /// 是否启用修改审计（记录最后修改时间、修改人）
    /// </summary>
    public bool EnableModificationAudit { get; set; } = true;

    /// <summary>
    /// 是否启用删除审计（记录删除时间、删除人）
    /// </summary>
    public bool EnableDeletionAudit { get; set; } = true;

    /// <summary>
    /// 是否在审计操作时发布领域事件
    /// </summary>
    public bool EmitEvents { get; set; } = false;
}

/// <summary>
/// 标记实体不参与审计的特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NoAuditAttribute : Attribute
{
}

/// <summary>
/// 标记属性在审计比较时忽略的特性
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAuditAttribute : Attribute
{
}
