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
/// 数据库优先的本地化器
/// </summary>
/// <remarks>
/// 每个回退区域先查数据库翻译的快照，再查嵌入的目录（按注册顺序）。快照由 <see cref="MessageTranslationLoader"/> 整份替换，读取无锁
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseMessageLocalizer : IMessageLocalizer, IMessageResolver
{
    /// <summary>
    /// 目录登记表
    /// </summary>
    /// <remarks>
    /// 同一键同一区域先找到的目录优先
    /// </remarks>
    private readonly MessageCatalogRegistry _catalogs;

    /// <summary>
    /// 当前快照
    /// </summary>
    /// <remarks>
    /// 整份替换
    /// </remarks>
    private volatile TranslationOverlay _overlay = TranslationOverlay.Empty;

    /// <summary>
    /// 用一组固定的目录创建
    /// </summary>
    /// <param name="catalogs">已注册的目录，按注册顺序</param>
    /// <param name="options">选项</param>
    public DatabaseMessageLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options)
        : this(new MessageCatalogRegistry(catalogs), options)
    {
    }

    /// <summary>
    /// 用目录登记表创建
    /// </summary>
    /// <remarks>
    /// 容器里由 <see cref="DatabaseLocalizationFeature"/> 按容器的登记表创建：运行期按归属登记与撤回的目录立即生效
    /// </remarks>
    /// <param name="catalogs">目录登记表</param>
    /// <param name="options">选项</param>
    public DatabaseMessageLocalizer(MessageCatalogRegistry catalogs, LocalizationOptions options)
    {
        _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public LocalizationOptions Options { get; }

    /// <summary>
    /// 当前快照里的翻译条数
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
    public string? Negotiate(string? locale) => locale is null ? null : Options.Negotiate([locale]);

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

    /// <summary>
    /// 整份替换快照
    /// </summary>
    internal void Apply(TranslationOverlay overlay) => _overlay = overlay;

    /// <summary>
    /// 沿回退链找模板：每个候选区域先看数据库快照，再看全部目录
    /// </summary>
    private bool TryFind(string key, string locale, out string template, out string resolvedLocale)
    {
        // 翻译与目录各取一份快照：并发的重新加载、登记与撤回不会让同一次查找前后看到不同的内容
        var overlay = _overlay;
        var catalogs = _catalogs.Catalogs;
        foreach (var candidate in Options.FallbackChain(locale))
        {
            if (overlay.TryGet(key, candidate, out template))
            {
                resolvedLocale = candidate;
                return true;
            }

            for (var index = 0; index < catalogs.Count; index++)
            {
                if (catalogs[index].Find(key, candidate).TryGetValue(out var found))
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
