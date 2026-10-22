// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 按消息键与区域解析文案的通用入口：任何地方（错误、通知、邮件、界面文本）都用它取当前调用方语言的文本。
/// 实现负责回退链（精确区域 → 同语言 → 默认区域 → 源语言 <c>en-US</c>）、按目标区域的文化格式化占位符，
/// 以及找不到键或占位符缺值时退回 <see cref="MessageTemplate.Describe"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageLocalizer
{
    /// <summary>
    /// 选项（默认区域与启用的区域）。
    /// </summary>
    LocalizationOptions Options { get; }

    /// <summary>
    /// 取模板（未格式化）；回退链用尽仍没有时无值。
    /// </summary>
    Maybe<string> Find(string key, string locale);

    /// <summary>
    /// 按区域解析并格式化，给出实际命中的区域；没有模板或占位符缺值时无值。
    /// </summary>
    Maybe<LocalizedText> Resolve(string key, string locale, IReadOnlyDictionary<string, object?>? arguments);

    /// <summary>
    /// 按区域取文案并格式化；没有模板或占位符缺值时返回键与参数的描述。
    /// </summary>
    string Localize(string key, string locale, IReadOnlyDictionary<string, object?>? arguments);

    /// <summary>
    /// 按调用上下文里的区域（Baggage <see cref="BaggageKeys.Locale"/>，缺失时为默认区域）取文案并格式化；便捷形式见 <c>context.Localize(key, arguments)</c> 与 <c>key.L()</c>。
    /// </summary>
    string Localize(string key, InvocationContext context, IReadOnlyDictionary<string, object?>? arguments);

    /// <summary>
    /// 把错误翻译成指定区域并给出命中的区域；自由文本错误或没有模板时无值。
    /// </summary>
    Maybe<LocalizedText> Resolve(Error failure, string locale);

    /// <summary>
    /// 把错误翻译成指定区域：键是 <see cref="Error.MessageKey"/>，参数是 <see cref="Error.Details"/>；自由文本错误返回其 <see cref="Error.Text"/>，没有模板时返回 <see cref="Error.Message"/>。
    /// </summary>
    string Localize(Error failure, string locale);

    /// <summary>
    /// 把错误翻译成调用上下文里的区域。
    /// </summary>
    string Localize(Error failure, InvocationContext context);
}
