// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions.Messages;

/// <summary>
/// 按消息键与区域解析文案的通用入口
/// </summary>
/// <remarks>
/// 任何地方（错误、通知、邮件、界面文本）都用它取当前调用方语言的文本。接口只有三个原语，按上下文取、翻译错误、退回描述这些形式由 <see cref="MessageLocalizerExtensions"/> 推出，各实现行为一致（ADR-0146）。实现负责回退链（精确区域 → 同语言 → 默认区域 → 源语言 <c>en-US</c>）、按目标区域的文化格式化占位符，以及找不到键或占位符缺值时退回 <see cref="MessageTemplate.Describe(string)"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageLocalizer
{
    /// <summary>
    /// 选项（默认区域与启用的区域）
    /// </summary>
    LocalizationOptions Options { get; }

    /// <summary>
    /// 取模板（未格式化）
    /// </summary>
    /// <remarks>
    /// 回退链用尽仍没有时无值
    /// </remarks>
    Maybe<string> Find(string key, string locale);

    /// <summary>
    /// 按区域解析并格式化，给出实际命中的区域
    /// </summary>
    /// <remarks>
    /// 没有模板或占位符缺值时无值
    /// </remarks>
    Maybe<LocalizedText> Resolve(string key, string locale, IReadOnlyDictionary<string, object?>? arguments);
}
