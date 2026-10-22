// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Profiles.Minimal;

/// <summary>
/// Minimal 组合：内核、宿主与本地化，不装配任何子系统。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MinimalProfile
{
    /// <summary>
    /// 装配 Minimal 组合：内存本地化默认实现；内核与宿主由 <c>AddXiHan()</c> 与 <c>UseXiHan()</c> 提供。
    /// </summary>
    public static AppBuilder UseMinimalProfile(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // 本地化是内核消息能被人读懂的前提：Minimal 也装配内存默认实现，让 key.L() 与 Problem Details 在最小组合下就有默认语言文案
        return builder.UseFeature<InMemoryLocalizationFeature>();
    }
}
