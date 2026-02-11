#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PartialTemplateInfo
// Guid:bec4b349-9f46-4a3c-ae46-6757d5978f5d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:14:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段信息
/// </summary>
public record PartialTemplateInfo
{
    /// <summary>
    /// 片段名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 片段路径
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 内容哈希
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// 依赖的片段
    /// </summary>
    public ICollection<string> Dependencies { get; init; } = [];

    /// <summary>
    /// 元数据
    /// </summary>
    public IDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}
