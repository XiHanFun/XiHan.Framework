// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 按消息键与区域解析文案的最小契约；由本地化子系统实现并安装到 <see cref="MessageScope"/>，供 <see cref="MessageKeyExtensions.L(string)"/> 与 <see cref="Error.Message"/> 在任何地方使用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageResolver
{
    /// <summary>
    /// 解析并格式化；<paramref name="locale"/> 为 <see langword="null"/> 时用实现的默认区域。找不到键或占位符缺值返回 <see langword="false"/>。
    /// </summary>
    bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text);
}
