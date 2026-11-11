// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions.Messages;

/// <summary>
/// 由 <see cref="IMessageLocalizer.Resolve(string, string, IReadOnlyDictionary{string, object?})"/> 推出的取文案形式
/// </summary>
/// <remarks>
/// 这些形式只做退回与区域选择，不碰模板：每个实现的行为因此一致，实现只需要提供 <see cref="IMessageLocalizer.Find"/> 与 <see cref="IMessageLocalizer.Resolve(string, string, IReadOnlyDictionary{string, object?})"/>（ADR-0146）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessageLocalizerExtensions
{
    /// <summary>
    /// 按区域取文案并格式化
    /// </summary>
    /// <remarks>
    /// 没有模板或占位符缺值时返回键与参数的描述（<see cref="MessageTemplate.Describe(string)"/>）
    /// </remarks>
    public static string Localize(this IMessageLocalizer localizer, string key, string locale, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return localizer.Resolve(key, locale, arguments).TryGetValue(out var localized) ? localized.Text : MessageTemplate.Describe(key, arguments);
    }

    /// <summary>
    /// 按调用上下文里的区域（Baggage <see cref="BaggageKeys.Locale"/>，缺失时为默认区域）取文案并格式化
    /// </summary>
    /// <remarks>
    /// 便捷形式见 <c>context.Localize(key, arguments)</c> 与 <c>key.L()</c>
    /// </remarks>
    public static string Localize(this IMessageLocalizer localizer, string key, InvocationContext context, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);
        return localizer.Localize(key, localizer.Options.Resolve(context), arguments);
    }

    /// <summary>
    /// 把错误翻译成指定区域并给出命中的区域
    /// </summary>
    /// <remarks>
    /// 键是 <see cref="Error.MessageKey"/>，参数是 <see cref="Error.Details"/>；自由文本错误或没有模板时无值
    /// </remarks>
    public static Maybe<LocalizedText> Resolve(this IMessageLocalizer localizer, Error failure, string locale)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(failure);
        // 自由文本错误（线上还原、异常转换）没有键，无法翻译
        return failure.MessageKey is null ? default : localizer.Resolve(failure.MessageKey, locale, failure.Details);
    }

    /// <summary>
    /// 把错误翻译成指定区域
    /// </summary>
    /// <remarks>
    /// 自由文本错误返回其 <see cref="Error.Text"/>，没有模板时返回 <see cref="Error.Message"/>
    /// </remarks>
    public static string Localize(this IMessageLocalizer localizer, Error failure, string locale)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(failure);
        return localizer.Resolve(failure, locale).TryGetValue(out var localized) ? localized.Text : failure.Message;
    }

    /// <summary>
    /// 把错误翻译成调用上下文里的区域
    /// </summary>
    public static string Localize(this IMessageLocalizer localizer, Error failure, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);
        return localizer.Localize(failure, localizer.Options.Resolve(context));
    }
}
