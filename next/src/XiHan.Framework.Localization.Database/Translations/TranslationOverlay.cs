// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 一份不可变的翻译快照。
/// </summary>
/// <remarks>
/// 键 → 区域 → 模板；整份替换，读取无锁。
/// </remarks>
internal sealed class TranslationOverlay
{
    /// <summary>
    /// 空快照。
    /// </summary>
    public static TranslationOverlay Empty { get; } = new(new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal), 0);

    /// <summary>
    /// 键 → 区域（忽略大小写）→ 模板。
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _templates;

    /// <summary>
    /// 由已分组的字典创建。
    /// </summary>
    private TranslationOverlay(Dictionary<string, Dictionary<string, string>> templates, int count)
    {
        _templates = templates;
        Count = count;
    }

    /// <summary>
    /// 翻译条数。
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 由翻译实体建立快照。
    /// </summary>
    /// <remarks>
    /// 同一键与区域重复时后者覆盖。
    /// </remarks>
    public static TranslationOverlay From(IEnumerable<MessageTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);
        var templates = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var count = 0;
        foreach (var translation in translations)
        {
            if (!templates.TryGetValue(translation.Key, out var byLocale))
            {
                byLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                templates[translation.Key] = byLocale;
            }

            if (byLocale.TryAdd(translation.Locale, translation.Template))
            {
                count++;
            }
            else
            {
                byLocale[translation.Locale] = translation.Template;
            }
        }

        return new TranslationOverlay(templates, count);
    }

    /// <summary>
    /// 取模板。
    /// </summary>
    public bool TryGet(string key, string locale, out string template)
    {
        if (_templates.TryGetValue(key, out var byLocale) && byLocale.TryGetValue(locale, out var found))
        {
            template = found;
            return true;
        }

        template = string.Empty;
        return false;
    }
}
