// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 文本没有通过读取检查的原因
/// </summary>
/// <remarks>
/// <see cref="JsonText.Check"/> 的结果（RFC-0001「JSON 文本」）
/// </remarks>
/// <param name="IsSyntax">是否是语法错误（不是 JSON、嵌套过深、字符串不是合法的 Unicode）；否则是重复成员</param>
/// <param name="Path">语法错误时为 <c>$</c>，重复成员时为第二次出现的成员的路径（写法同 RFC-0011 规则 2）</param>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct JsonTextProblem(bool IsSyntax, string Path);
