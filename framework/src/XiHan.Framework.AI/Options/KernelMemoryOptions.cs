#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KernelMemoryOptions
// Guid:8aae3584-9e7e-4661-8242-b72857e53bb1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 内核记忆配置
/// </summary>
public class KernelMemoryOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:AI:Memory";

    /// <summary>
    /// 存储类型
    /// </summary>
    public string StorageType { get; set; } = "Volatile";

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 嵌入模型名称
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// 向量维度
    /// </summary>
    public int VectorSize { get; set; } = 1536;
}
