// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Persistence.Generators;

/// <summary>
/// 一个待生成的实体：类型、键类型、实现的协议与是否生成过滤规约。
/// </summary>
internal sealed class EntityModel
{
    /// <summary>
    /// 创建。
    /// </summary>
    public EntityModel(INamedTypeSymbol type, ITypeSymbol keyType, bool isSoftDeletable, bool isMultiTenant, bool generateFilters)
    {
        Type = type;
        KeyType = keyType;
        IsSoftDeletable = isSoftDeletable;
        IsMultiTenant = isMultiTenant;
        GenerateFilters = generateFilters;
    }

    /// <summary>
    /// 实体类型。
    /// </summary>
    public INamedTypeSymbol Type { get; }

    /// <summary>
    /// 键类型。
    /// </summary>
    public ITypeSymbol KeyType { get; }

    /// <summary>
    /// 是否实现 <c>ISoftDeletable</c>。
    /// </summary>
    public bool IsSoftDeletable { get; }

    /// <summary>
    /// 是否实现 <c>IMultiTenant</c>。
    /// </summary>
    public bool IsMultiTenant { get; }

    /// <summary>
    /// 是否生成 <c>&lt;实体&gt;Filters</c>（实现了软删除或租户协议且没有同名冲突）。
    /// </summary>
    public bool GenerateFilters { get; }

    /// <summary>
    /// 过滤规约类名。
    /// </summary>
    public string FiltersName => Type.Name + "Filters";
}
