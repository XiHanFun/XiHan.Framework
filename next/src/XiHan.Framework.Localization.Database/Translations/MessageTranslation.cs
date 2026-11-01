// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 一条翻译。
/// </summary>
/// <remarks>
/// 键与区域共同唯一（唯一索引 <c>(Key, Locale)</c>，按键与区域查找也走它），模板与语言文件里的写法相同。EF Core 应用在自己的 <c>DbContext</c> 里 <c>modelBuilder.Entity&lt;MessageTranslation&gt;()</c> 后调用 <c>ApplyPersistenceConventions(Database)</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[EntityIndex(nameof(Key), nameof(Locale), IsUnique = true)]
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
    /// <remarks>
    /// 列长取区域名文法的上限 <see cref="LocaleName.MaxLength"/>，合法的区域名都能写入。
    /// </remarks>
    [StringLength(LocaleName.MaxLength)]
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// 模板。
    /// </summary>
    [LongText]
    public string Template { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; set; }
}
