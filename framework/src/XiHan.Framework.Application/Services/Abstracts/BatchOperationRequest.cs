#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BatchOperationRequest
// Guid:k1l2m3n4-o5p6-4q7r-8s9t-0u1v2w3x4y5z
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Services.Abstracts;

/// <summary>
/// 批量操作请求
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class BatchOperationRequest<T>
{
    /// <summary>
    /// 操作数据列表
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// 是否在遇到错误时继续执行
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// 是否使用事务
    /// </summary>
    public bool UseTransaction { get; set; } = true;
}

/// <summary>
/// 批量删除请求
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public class BatchDeleteRequest<TKey>
{
    /// <summary>
    /// 要删除的主键列表
    /// </summary>
    public List<TKey> Ids { get; set; } = [];

    /// <summary>
    /// 是否在遇到错误时继续执行
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// 是否使用事务
    /// </summary>
    public bool UseTransaction { get; set; } = true;

    /// <summary>
    /// 是否软删除
    /// </summary>
    public bool SoftDelete { get; set; } = true;
}

/// <summary>
/// 批量更新请求
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUpdate">更新数据类型</typeparam>
public class BatchUpdateRequest<TKey, TUpdate>
{
    /// <summary>
    /// 更新项列表
    /// </summary>
    public List<BatchUpdateItem<TKey, TUpdate>> Items { get; set; } = [];

    /// <summary>
    /// 是否在遇到错误时继续执行
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// 是否使用事务
    /// </summary>
    public bool UseTransaction { get; set; } = true;
}

/// <summary>
/// 批量更新项
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUpdate">更新数据类型</typeparam>
public class BatchUpdateItem<TKey, TUpdate>
{
    /// <summary>
    /// 主键
    /// </summary>
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// 更新数据
    /// </summary>
    public TUpdate Data { get; set; } = default!;
}
