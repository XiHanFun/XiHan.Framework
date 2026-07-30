// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
