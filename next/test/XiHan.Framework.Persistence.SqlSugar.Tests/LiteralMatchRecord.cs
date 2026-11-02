// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 字符串按字面匹配的用例实体。
/// </summary>
/// <remarks>
/// 名字里带 <c>LIKE</c> 的通配符、转义符与方括号；片段是另一列，用来验证实参是列的翻译。
/// </remarks>
[SugarTable("literal_match_record")]
public sealed class LiteralMatchRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 被匹配的名字。
    /// </summary>
    [StringLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 作为实参的另一列。
    /// </summary>
    [StringLength(64)]
    public string Fragment { get; set; } = string.Empty;
}
