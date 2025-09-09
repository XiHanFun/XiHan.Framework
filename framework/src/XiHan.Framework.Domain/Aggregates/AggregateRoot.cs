#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AggregateRoot
// Guid:aaa45b10-aa93-4958-9af0-063ac97ffb26
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 4:35:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Aggregates;

/// <summary>
/// 聚合根
/// </summary>
public abstract class AggregateRoot : AggregateRootBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected AggregateRoot()
    {
    }
}

/// <summary>
/// 泛型主键聚合根
/// </summary>
/// <typeparam name="TKey"></typeparam>
public abstract class AggregateRoot<TKey> : AggregateRootBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected AggregateRoot()
    {
    }
}
