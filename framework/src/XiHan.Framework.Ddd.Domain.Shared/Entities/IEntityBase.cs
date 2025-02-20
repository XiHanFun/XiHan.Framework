#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEntityBase
// Guid:34b2a763-dc44-4fbd-a625-b650baf5d5d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 2:48:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Ddd.Domain.Shared.Entities;

/// <summary>
/// 实体基类接口
/// </summary>
public interface IEntityBase
{
}

/// <summary>
/// 泛型主键实体基类接口
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IEntityBase<TKey> : IEntityBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    TKey BasicId { get; }
}
