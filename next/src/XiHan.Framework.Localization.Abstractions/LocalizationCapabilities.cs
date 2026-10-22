// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 本地化子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class LocalizationCapabilities
{
    /// <summary>消息资源：按键与区域取文案。</summary>
    public const string Resources = "localization.resources";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
