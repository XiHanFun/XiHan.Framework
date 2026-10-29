// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 数据库优先的本地化器。
/// </summary>
/// <remarks>
/// 每个回退区域先查数据库翻译的快照，再查嵌入的目录（按注册顺序）。快照由 <see cref="MessageTranslationLoader"/> 整份替换，读取无锁。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseMessageLocalizer : IMessageLocalizer, IMessageResolver
{
    /// <summary>
    /// 已注册的目录，按注册顺序。
    /// </summary>
    /// <remarks>
    /// 同一键先注册的目录优先。
    /// </remarks>
    private readonly IReadOnlyList<MessageCatalog> _catalogs;

    /// <summary>
    /// 当前快照。
    /// </summary>
    /// <remarks>
    /// 整份替换。
    /// </remarks>
    private volatile TranslationOverlay _overlay = TranslationOverlay.Empty;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseMessageLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        _catalogs = catalogs.ToList();
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public LocalizationOptions Options { get; }

    /// <summary>
    /// 当前快照里的翻译条数。
    /// </summary>
    public int TranslationCount => _overlay.Count;

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
            && MessageTemplate.TryFormat(template, arguments, LocaleName.CultureOf(resolvedLocale), out text))
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
            && MessageTemplate.TryFormat(template, arguments, LocaleName.CultureOf(resolvedLocale), out var text)
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
    /// 整份替换快照。
    /// </summary>
    internal void Apply(TranslationOverlay overlay) => _overlay = overlay;

    /// <summary>
    /// 沿回退链找模板：每个候选区域先看数据库快照，再看全部目录。
    /// </summary>
    private bool TryFind(string key, string locale, out string template, out string resolvedLocale)
    {
        var overlay = _overlay;
        foreach (var candidate in Options.FallbackChain(locale))
        {
            if (overlay.TryGet(key, candidate, out template))
            {
                resolvedLocale = candidate;
                return true;
            }

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
}
