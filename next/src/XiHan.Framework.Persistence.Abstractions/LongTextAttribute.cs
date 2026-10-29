// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 标记字符串属性映射为数据库的长文本列。
/// </summary>
/// <remarks>
/// PostgreSQL <c>text</c>、SQL Server <c>nvarchar(max)</c>、MySQL <c>longtext</c> 等。不限长度。JSON 文档、模板、错误文案这类列用它。有上限的字符串列用 <c>[StringLength(n)]</c>。Provider 据此建表：SqlSugar 的约定映射为各库的长文本类型，EF Core 对没有上限的字符串本来就建长文本。不用 DataAnnotations 的无参 <c>[MaxLength]</c>：它的构造函数带 <c>RequiresUnreferencedCode</c>，AOT 分析器不放行。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LongTextAttribute : Attribute
{
}
