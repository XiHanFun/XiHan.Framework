// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 结果原语的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class ResultMessages
{
    /// <summary>使用了未初始化的结果（<c>default</c> 值）。</summary>
    public const string Uninitialized = "primitives.result.uninitialized";
}
