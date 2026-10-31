// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 按消息键与区域解析文案的最小契约。
/// </summary>
/// <remarks>
/// 由本地化子系统实现并安装到 <see cref="MessageScope"/>，供 <see cref="MessageKeyExtensions.L(string)"/> 与 <see cref="Error.Message"/> 在任何地方使用。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageResolver
{
    /// <summary>
    /// 解析并格式化。
    /// </summary>
    /// <remarks>
    /// <paramref name="locale"/> 为 <see langword="null"/> 时用实现的默认区域。找不到键或占位符缺值返回 <see langword="false"/>。
    /// </remarks>
    bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text);

    /// <summary>
    /// 把调用方给出的区域协商成实现启用的区域。
    /// </summary>
    /// <remarks>
    /// 内核在调用入口用它决定 <see cref="MessageScope"/> 的区域，使 <c>key.L()</c> 与按调用上下文解析的文案（Problem Details 等）是同一个区域（RFC-0010）。<paramref name="locale"/> 为 <see langword="null"/> 时返回 <see langword="null"/>，由实现用默认区域；缺省实现原样返回，本地化 Provider 按启用列表协商。
    /// </remarks>
    string? Negotiate(string? locale) => locale;
}
