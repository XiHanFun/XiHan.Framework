// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core;

/// <summary>
/// 命名类型选择器
/// </summary>
public class NamedTypeSelector
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">Name</param>
    /// <param name="predicate">Predicate</param>
    public NamedTypeSelector(string name, Func<Type, bool> predicate)
    {
        Name = name;
        Predicate = predicate;
    }

    /// <summary>
    /// 选择器名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 断言
    /// </summary>
    public Func<Type, bool> Predicate { get; set; }
}
