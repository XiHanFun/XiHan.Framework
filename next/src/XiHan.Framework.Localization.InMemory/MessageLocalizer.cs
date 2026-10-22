// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.InMemory;

/// <summary>
/// 汇集全部已注册目录的本地化器，同时是安装到 <see cref="MessageScope"/> 的解析器。
/// 回退链：精确区域 → 同语言的其他启用区域 → 默认区域 → 源语言 <c>en-US</c>；同一键先注册的目录优先。
/// 占位符按命中区域的文化格式化（<c>{amount:N2}</c> 在 <c>de-DE</c> 得到 <c>1.234,50</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MessageLocalizer : IMessageLocalizer, IMessageResolver
{
    /// <summary>
    /// 已注册的目录，按注册顺序；同一键先注册的目录优先。
    /// </summary>
    private readonly IReadOnlyList<MessageCatalog> _catalogs;

    /// <summary>
    /// 创建本地化器。
    /// </summary>
    /// <param name="catalogs">已注册的目录，按注册顺序。</param>
    /// <param name="options">选项。</param>
    public MessageLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        _catalogs = catalogs.ToList();
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public LocalizationOptions Options { get; }

    /// <inheritdoc />
    public Maybe<string> Find(string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(locale);
        return TryFind(key, locale, out var template, out _) ? Maybe.Some(template) : default;
    }

    /// <inheritdoc />
    public bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(key);
        // 没有模板，或模板的占位符缺值，都算解析失败，由调用方退回键与参数的描述，绝不输出半截文案
        if (TryFind(key, locale ?? Options.DefaultLocale, out var template, out var resolvedLocale)
            && MessageTemplate.TryFormat(template, arguments, CultureOf(resolvedLocale), out text))
        {
            return true;
        }

        text = null;
        return false;
    }

    /// <inheritdoc />
    public Maybe<LocalizedText> Resolve(string key, string locale, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(locale);
        return TryFind(key, locale, out var template, out var resolvedLocale)
            && MessageTemplate.TryFormat(template, arguments, CultureOf(resolvedLocale), out var text)
            ? Maybe.Some(new LocalizedText(text, resolvedLocale))
            : default;
    }

    /// <inheritdoc />
    public string Localize(string key, string locale, IReadOnlyDictionary<string, object?>? arguments)
        => Resolve(key, locale, arguments).TryGetValue(out var localized) ? localized.Text : MessageTemplate.Describe(key, arguments);

    /// <inheritdoc />
    public string Localize(string key, InvocationContext context, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Localize(key, Options.Resolve(context), arguments);
    }

    /// <inheritdoc />
    public Maybe<LocalizedText> Resolve(Error failure, string locale)
    {
        ArgumentNullException.ThrowIfNull(failure);
        // 自由文本错误（线上还原、异常转换）没有键，无法翻译
        return failure.MessageKey is null ? default : Resolve(failure.MessageKey, locale, failure.Details);
    }

    /// <inheritdoc />
    public string Localize(Error failure, string locale)
        => Resolve(failure, locale).TryGetValue(out var localized) ? localized.Text : failure.Message;

    /// <inheritdoc />
    public string Localize(Error failure, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Localize(failure, Options.Resolve(context));
    }

    /// <summary>
    /// 沿回退链在全部目录里找模板，返回命中的模板与其区域。
    /// </summary>
    private bool TryFind(string key, string locale, out string template, out string resolvedLocale)
    {
        foreach (var candidate in FallbackChain(locale))
        {
            foreach (var catalog in _catalogs)
            {
                if (catalog.Find(key, candidate).TryGetValue(out var found))
                {
                    template = found;
                    resolvedLocale = candidate;
                    return true;
                }
            }
        }

        template = string.Empty;
        resolvedLocale = string.Empty;
        return false;
    }

    /// <summary>
    /// 回退链：请求的区域（规范化）→ 启用列表里同语言的其他区域 → 默认区域 → 源语言；去重并保持顺序。
    /// </summary>
    private IEnumerable<string> FallbackChain(string locale)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requested = LocaleName.IsValid(locale) ? LocaleName.Normalize(locale) : Options.DefaultLocale;
        if (seen.Add(requested))
        {
            yield return requested;
        }

        var language = LocaleName.LanguageOf(requested);
        foreach (var enabled in Options.Locales)
        {
            if (string.Equals(LocaleName.LanguageOf(enabled), language, StringComparison.Ordinal) && seen.Add(enabled))
            {
                yield return enabled;
            }
        }

        if (seen.Add(Options.DefaultLocale))
        {
            yield return Options.DefaultLocale;
        }

        // 源语言是代码与语言文件的基准，永远是最后一道回退
        if (seen.Add(SupportedLocales.Default))
        {
            yield return SupportedLocales.Default;
        }
    }

    /// <summary>
    /// 区域对应的文化；运行时不认识的区域（或不变全球化模式）用不变文化。
    /// </summary>
    private static CultureInfo CultureOf(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
