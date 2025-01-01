#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NamedTypeSelector
// Guid:fed2d460-3b5f-463a-a696-bf865d920877
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 4:07:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 命名类型选择器
/// </summary>
public class NamedTypeSelector
{
    /// <summary>
    /// 选择器名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 断言
    /// </summary>
    public Func<Type, bool> Predicate { get; set; }

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
}
