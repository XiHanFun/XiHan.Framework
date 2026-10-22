// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 编译后的条件集（不可变，可跨请求共用）
/// </summary>
public sealed class CompiledConditionSet
{
    internal CompiledConditionSet(
        IReadOnlyList<CompiledConditionGroup> groups,
        IReadOnlyList<ConditionCompileError> errors,
        TimeZoneInfo timeZone)
    {
        Groups = groups;
        Errors = errors;
        TimeZone = timeZone;
        ReferencedAttributes = groups
            .SelectMany(static group => group.Clauses)
            .Where(static clause => clause.Resolution is not null)
            .Select(static clause => clause.Resolution!.CanonicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 非空的组
    /// </summary>
    public IReadOnlyList<CompiledConditionGroup> Groups { get; }

    /// <summary>
    /// 编译错误；为空表示全部子句编译成功（保存前据此拒绝）
    /// </summary>
    public IReadOnlyList<ConditionCompileError> Errors { get; }

    /// <summary>
    /// 是否「无条件」（没有非空组），求值恒为命中
    /// </summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>
    /// 被引用到的属性规范名（可据此预取属性、判断求值时机）
    /// </summary>
    public IReadOnlySet<string> ReferencedAttributes { get; }

    /// <summary>
    /// 是否有子句用了旧名别名
    /// </summary>
    public bool UsesAlias => Groups.SelectMany(static group => group.Clauses).Any(static clause => clause.Resolution?.IsAlias == true);

    internal TimeZoneInfo TimeZone { get; }
}
