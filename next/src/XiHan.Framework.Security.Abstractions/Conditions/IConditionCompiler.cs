// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件编译器：按属性登记解析条件值，检查运算符与值种类是否搭配。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IConditionCompiler
{
    /// <summary>
    /// 编译条件集；出错的子句保留在结果里并记入 <see cref="CompiledConditionSet.Errors"/>。<paramref name="options"/> 为 <see langword="null"/> 时取 <see cref="ConditionCompileOptions.Default"/>。
    /// </summary>
    CompiledConditionSet Compile(ConditionSet conditions, ConditionCompileOptions? options = null);
}
