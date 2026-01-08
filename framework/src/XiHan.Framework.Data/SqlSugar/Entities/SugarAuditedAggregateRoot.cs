#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarAuditedAggregateRoot
// Guid:a37f8d95-3522-474c-aadc-bbb1dad2482c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/9 6:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 完整审计聚合根基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarAuditedAggregateRoot<TKey> : SugarFullAuditedEntity<TKey>
     where TKey : IEquatable<TKey>
{
}
