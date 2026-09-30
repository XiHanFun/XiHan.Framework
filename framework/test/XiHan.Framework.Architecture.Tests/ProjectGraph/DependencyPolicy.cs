// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 一条允许的向上引用
/// </summary>
/// <param name="From">引用方项目名</param>
/// <param name="To">被引用项目名</param>
/// <param name="Reason">登记理由</param>
internal sealed record DependencyException(string From, string To, string Reason);

/// <summary>
/// 依赖规则的例外清单
/// </summary>
internal sealed class DependencyPolicy
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="exceptions">允许的向上引用</param>
    public DependencyPolicy(IEnumerable<DependencyException> exceptions)
    {
        Exceptions = [.. exceptions];
    }

    /// <summary>
    /// 允许的向上引用
    /// </summary>
    public IReadOnlyList<DependencyException> Exceptions { get; }

    /// <summary>
    /// 判断依赖边是否登记为例外
    /// </summary>
    /// <param name="edge">依赖边</param>
    /// <returns>已登记返回 true</returns>
    public bool Allows(ProjectEdge edge)
    {
        return Exceptions.Any(item =>
            string.Equals(item.From, edge.From, StringComparison.Ordinal)
            && string.Equals(item.To, edge.To, StringComparison.Ordinal));
    }
}
