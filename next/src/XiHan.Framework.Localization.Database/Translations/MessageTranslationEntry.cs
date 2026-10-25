// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 要写入的一条翻译；键须符合消息键文法，区域会规范化大小写，模板不能为空。不合法是编程错误，在构造时抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record MessageTranslationEntry
{
    /// <summary>
    /// 创建。
    /// </summary>
    public MessageTranslationEntry(string key, string locale, string template)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        if (!MessageTemplate.IsValidKey(key))
        {
            throw new ArgumentException($"Message key '{key}' is invalid: it must match ^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$.", nameof(key));
        }

        Key = key;
        Locale = LocaleName.Normalize(LocaleName.Validate(locale));
        Template = template;
    }

    /// <summary>
    /// 消息键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 区域（已规范化）。
    /// </summary>
    public string Locale { get; }

    /// <summary>
    /// 模板。
    /// </summary>
    public string Template { get; }
}
