// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 只读的消息目录：消息键 → 区域 → 模板。一种语言一个文件（<c>Localization/&lt;区域&gt;.json</c>，内容是键 → 模板的平面对象），
/// 各包用 <see cref="FromEmbeddedResources"/> 装载自己嵌入的全部语言文件。区域名在装载时规范化，查找不区分大小写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageCatalog
{
    /// <summary>
    /// 嵌入资源的默认前缀。
    /// </summary>
    public const string DefaultResourcePrefix = "Localization/";

    /// <summary>
    /// 消息键 → 区域（规范化、不区分大小写）→ 模板。
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _byKey;

    /// <summary>
    /// 目录里出现过的区域，按装载顺序。
    /// </summary>
    private readonly List<string> _locales;

    /// <summary>
    /// 用按区域分组的模板创建目录；键与区域名会被校验，区域名规范化。
    /// </summary>
    /// <param name="name">目录名，同名目录只注册一次。</param>
    /// <param name="templatesByLocale">区域 → 消息键 → 模板。</param>
    public MessageCatalog(string name, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> templatesByLocale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(templatesByLocale);
        Name = name;
        _byKey = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        _locales = [];
        foreach (var (locale, templates) in templatesByLocale)
        {
            if (!LocaleName.IsValid(locale))
            {
                throw Invalid(name, $"locale '{locale}' is not valid");
            }

            ArgumentNullException.ThrowIfNull(templates);
            var normalized = LocaleName.Normalize(locale);
            if (_locales.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw Invalid(name, $"locale '{normalized}' is listed more than once");
            }

            _locales.Add(normalized);
            foreach (var (key, template) in templates)
            {
                if (!MessageTemplate.IsValidKey(key))
                {
                    throw Invalid(name, $"message key '{key}' in locale '{normalized}' is not valid");
                }

                if (template is null)
                {
                    throw Invalid(name, $"template for key '{key}' in locale '{normalized}' is null");
                }

                if (!_byKey.TryGetValue(key, out var byLocale))
                {
                    byLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _byKey[key] = byLocale;
                }

                byLocale[normalized] = template;
            }
        }
    }

    /// <summary>
    /// 目录名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 目录里出现过的区域，按装载顺序。
    /// </summary>
    public IReadOnlyCollection<string> Locales => _locales;

    /// <summary>
    /// 全部消息键。
    /// </summary>
    public IReadOnlyCollection<string> Keys => _byKey.Keys;

    /// <summary>
    /// 消息键的数量。
    /// </summary>
    public int Count => _byKey.Count;

    /// <summary>
    /// 从 <paramref name="anchor"/> 所在程序集的嵌入资源装载全部 <c>&lt;前缀&gt;&lt;区域&gt;.json</c>；没有任何语言文件或文件不合法时抛出携带 <see cref="LocalizationErrors.InvalidCatalog"/> 的 <see cref="FrameworkException"/>。
    /// </summary>
    /// <param name="name">目录名。</param>
    /// <param name="anchor">目录所在程序集里的任意类型，用于定位程序集。</param>
    /// <param name="prefix">嵌入资源的逻辑名前缀，默认 <see cref="DefaultResourcePrefix"/>。</param>
    public static MessageCatalog FromEmbeddedResources(string name, Type anchor, string prefix = DefaultResourcePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(prefix);
        var assembly = anchor.Assembly;
        var templatesByLocale = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            // 只认 <前缀><区域>.json 形态的资源，文件名就是区域名
            if (!resource.StartsWith(prefix, StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var locale = resource[prefix.Length..^".json".Length];
            if (!LocaleName.IsValid(locale))
            {
                throw Invalid(name, $"embedded resource '{resource}' is not named after a valid locale");
            }

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw Invalid(name, $"embedded resource '{resource}' could not be opened");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            templatesByLocale[LocaleName.Normalize(locale)] = ParseLocaleFile(name, locale, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        }

        if (templatesByLocale.Count == 0)
        {
            throw Invalid(name, $"assembly '{assembly.GetName().Name}' has no embedded '{prefix}<locale>.json' resources");
        }

        return new MessageCatalog(name, templatesByLocale);
    }

    /// <summary>
    /// 解析一种语言的文件（键 → 模板的平面 JSON 对象）；不合法抛出携带 <see cref="LocalizationErrors.InvalidCatalog"/> 的 <see cref="FrameworkException"/>。
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseLocaleFile(string name, string locale, ReadOnlySpan<byte> utf8Json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(locale);
        Dictionary<string, string>? document;
        try
        {
            document = JsonSerializer.Deserialize(utf8Json, LocalizationJsonContext.Default.DictionaryStringString);
        }
        catch (JsonException exception)
        {
            throw new FrameworkException(LocalizationErrors.InvalidCatalog, $"Message catalog '{name}' locale '{locale}' is not valid JSON: {exception.Message}", exception);
        }

        if (document is null)
        {
            throw Invalid(name, $"locale '{locale}' document is null");
        }

        foreach (var (key, template) in document)
        {
            if (!MessageTemplate.IsValidKey(key))
            {
                throw Invalid(name, $"message key '{key}' in locale '{locale}' is not valid");
            }

            if (template is null)
            {
                throw Invalid(name, $"template for key '{key}' in locale '{locale}' is null");
            }
        }

        return document;
    }

    /// <summary>
    /// 取某键在某区域的模板，只做精确匹配；回退由 <see cref="IMessageLocalizer"/> 负责。
    /// </summary>
    public Maybe<string> Find(string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(locale);
        return _byKey.TryGetValue(key, out var templates) && templates.TryGetValue(locale, out var template)
            ? Maybe.Some(template)
            : default;
    }

    /// <summary>
    /// 某键已有模板的区域。
    /// </summary>
    public IReadOnlyCollection<string> LocalesOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _byKey.TryGetValue(key, out var templates) ? templates.Keys : [];
    }

    /// <summary>
    /// 构造"目录不合法"异常。
    /// </summary>
    private static FrameworkException Invalid(string name, string reason)
        => new(LocalizationErrors.InvalidCatalog, $"Message catalog '{name}' is not valid: {reason}.");
}
