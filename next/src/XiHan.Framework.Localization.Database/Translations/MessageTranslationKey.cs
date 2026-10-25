// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 翻译的自然键：消息键与区域。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record MessageTranslationKey
{
    /// <summary>
    /// 创建；区域会规范化大小写。
    /// </summary>
    public MessageTranslationKey(string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!MessageTemplate.IsValidKey(key))
        {
            throw new ArgumentException($"Message key '{key}' is invalid: it must match ^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$.", nameof(key));
        }

        Key = key;
        Locale = LocaleName.Normalize(LocaleName.Validate(locale));
    }

    /// <summary>
    /// 消息键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 区域（已规范化）。
    /// </summary>
    public string Locale { get; }
}
