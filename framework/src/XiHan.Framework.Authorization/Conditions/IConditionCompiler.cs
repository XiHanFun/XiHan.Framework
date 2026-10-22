// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件编译器：按属性登记解析条件值，检查运算符与值种类是否匹配
/// </summary>
public interface IConditionCompiler
{
    /// <summary>
    /// 编译条件集
    /// </summary>
    /// <param name="set">条件集</param>
    /// <param name="options">编译选项；为空取 <see cref="ConditionCompileOptions.Default"/></param>
    /// <returns>编译结果；出错的子句保留在结果里并记入 <see cref="CompiledConditionSet.Errors"/></returns>
    CompiledConditionSet Compile(ConditionSet set, ConditionCompileOptions? options = null);
}
