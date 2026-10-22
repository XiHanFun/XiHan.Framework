// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 一次解析的结果：格式化后的文案与实际命中的区域（可能是回退链上的区域，而不是请求的区域）。宿主据此设置 <c>Content-Language</c>。
/// </summary>
/// <param name="Text">格式化后的文案。</param>
/// <param name="Locale">实际命中的区域。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record LocalizedText(string Text, string Locale);
