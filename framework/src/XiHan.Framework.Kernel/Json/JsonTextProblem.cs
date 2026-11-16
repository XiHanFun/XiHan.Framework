// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 文本没有通过读取检查的原因
/// </summary>
/// <param name="IsSyntax">是否是语法错误（不是 JSON、嵌套过深、字符串不是合法的 Unicode）；否则是重复成员</param>
/// <param name="Path">语法错误时为 <c>$</c>，重复成员时为第二次出现的成员的路径</param>
internal readonly record struct JsonTextProblem(bool IsSyntax, string Path);
