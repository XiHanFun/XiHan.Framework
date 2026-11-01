// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 解析后的索引：名字、列与唯一性。
/// </summary>
/// <remarks>
/// 由 <see cref="EntityIndexes.Resolve"/> 从 <see cref="EntityIndexAttribute"/> 推出，Provider 原样建索引。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityIndex
{
    /// <summary>
    /// 由解析过程创建。
    /// </summary>
    internal EntityIndex(string name, IReadOnlyList<string> properties, bool isUnique)
    {
        Name = name;
        Properties = properties;
        IsUnique = isUnique;
    }

    /// <summary>
    /// 索引名。
    /// </summary>
    /// <remarks>
    /// 不超过 <see cref="EntityIndexes.MaxNameLength"/> 个字符。
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// 列（属性名），按顺序。
    /// </summary>
    /// <remarks>
    /// 软删除实体的唯一索引已含末尾的删除标记列。
    /// </remarks>
    public IReadOnlyList<string> Properties { get; }

    /// <summary>
    /// 是否唯一。
    /// </summary>
    public bool IsUnique { get; }
}
