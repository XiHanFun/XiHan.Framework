// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 一条翻译：键与区域共同唯一，模板与语言文件里的写法相同。EF Core 应用在自己的 <c>DbContext</c> 里 <c>modelBuilder.Entity&lt;MessageTranslation&gt;()</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MessageTranslation : IEntity<Guid>, IHasModificationTime
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 消息键。
    /// </summary>
    [StringLength(256)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 区域（规范化大小写，如 <c>zh-CN</c>）。
    /// </summary>
    [StringLength(16)]
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// 模板。
    /// </summary>
    [LongText]
    public string Template { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; set; }
}
