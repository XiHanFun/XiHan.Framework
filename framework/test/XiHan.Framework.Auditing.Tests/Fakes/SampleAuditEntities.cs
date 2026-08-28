// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// 这些样例实体故意放在 XiHan.Framework.Auditing 之外的命名空间：
// DefaultEntityAuditContextProvider.ShouldAudit 以 FullName 前缀 "XiHan.Framework.Auditing" 排除框架自身类型，
// 若样例类型落在测试工程默认命名空间（XiHan.Framework.Auditing.Tests）会被该前缀先行命中，
// 就无法分别验证 AuditLog / DiffLog 关键字这两条独立分支。
namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 普通业务实体样例，应当被审计
/// </summary>
public class SampleOrder
{
    /// <summary>
    /// 订单编号
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;
}

/// <summary>
/// 名称含 AuditLog 的实体样例，不应被审计
/// </summary>
public class SampleAuditLogEntity
{
    /// <summary>
    /// 标识
    /// </summary>
    public long Id { get; set; }
}

/// <summary>
/// 名称含 DiffLog 的实体样例，不应被审计
/// </summary>
public class SampleEntityDiffLogEntity
{
    /// <summary>
    /// 标识
    /// </summary>
    public long Id { get; set; }
}
