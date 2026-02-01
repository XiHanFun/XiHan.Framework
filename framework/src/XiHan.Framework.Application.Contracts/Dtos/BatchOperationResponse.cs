#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BatchOperationResponse
// Guid:def85f84-74ca-4a56-b6b7-ccec45c1d2bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 批量操作响应
/// </summary>
/// <typeparam name="T">结果类型</typeparam>
public class BatchOperationResponse<T>
{
    /// <summary>
    /// 成功数量
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsAllSuccess => FailureCount == 0;

    /// <summary>
    /// 结果列表
    /// </summary>
    public List<BatchOperationResult<T>> Results { get; set; } = [];

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// 批量操作结果
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class BatchOperationResult<T>
{
    /// <summary>
    /// 索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 结果数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }
}
