// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Profiles.Minimal;

/// <summary>
/// Minimal 组合：内核与宿主，不装配任何子系统。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MinimalProfile
{
    /// <summary>
    /// 装配 Minimal 组合。当前不添加特性，只保留入口。
    /// </summary>
    public static AppBuilder UseMinimalProfile(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
