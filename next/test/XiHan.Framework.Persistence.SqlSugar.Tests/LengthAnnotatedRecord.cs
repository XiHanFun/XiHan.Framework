// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 声明了列长的实体。
/// </summary>
/// <remarks>
/// 框架的 <c>[LongText]</c>、DataAnnotations 的 <c>[MaxLength(n)]</c> / 无参 <c>[MaxLength]</c> / <c>[StringLength(n)]</c>，以及没有注解的字符串列。
/// </remarks>
internal sealed class LengthAnnotatedRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 定长编码。
    /// </summary>
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 以无参 <c>[MaxLength]</c> 声明的长文本。
    /// </summary>
    /// <remarks>
    /// 应用实体可用；正式包里 AOT 分析器不放行。
    /// </remarks>
    [MaxLength]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 以框架 <c>[LongText]</c> 声明的长文本。
    /// </summary>
    [LongText]
    public string Document { get; set; } = string.Empty;

    /// <summary>
    /// 以 <c>[StringLength]</c> 声明的标签。
    /// </summary>
    [StringLength(10)]
    public string? Tag { get; set; }

    /// <summary>
    /// 没有注解的列：沿用 SqlSugar 默认。
    /// </summary>
    public string Plain { get; set; } = string.Empty;
}
