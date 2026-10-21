// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Profiles.Standard;

/// <summary>
/// Standard 组合：Minimal 加五个子系统的内存默认实现。显式 Provider 顶替同能力的默认实现。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class StandardProfile
{
    /// <summary>
    /// 装配 Standard 组合：持久化、缓存、多租户、安全、消息的内存默认实现。
    /// </summary>
    public static AppBuilder UseStandardProfile(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UseMinimalProfile()
            .UseFeature<InMemoryPersistenceFeature>()
            .UseFeature<InMemoryCachingFeature>()
            .UseFeature<InMemoryMultiTenancyFeature>()
            .UseFeature<InMemorySecurityFeature>()
            .UseFeature<InMemoryMessagingFeature>();
    }
}
