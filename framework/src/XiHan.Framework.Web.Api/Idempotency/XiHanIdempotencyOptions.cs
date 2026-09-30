// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等保护配置
/// </summary>
public class XiHanIdempotencyOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:Idempotency";

    /// <summary>
    /// 携带幂等键的请求头名称
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// 幂等键最大长度（字符）
    /// </summary>
    public int MaxKeyLength { get; set; } = 128;

    /// <summary>
    /// 参与摘要的请求参数序列化后的最大字节数
    /// </summary>
    public int MaxRequestBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// 单个响应快照的最大字节数
    /// </summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// 进程内存储的最大记录数
    /// </summary>
    public int MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// 进程内存储的响应快照总字节上限
    /// </summary>
    public long MaxTotalResponseBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// 完成记录的保留时长
    /// </summary>
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// 事务型端点处理中记录的租约时长，过期后允许重新取得（仅落库存储使用）
    /// </summary>
    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(5);
}
