// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 序列化器的注册规则，供需要序列化的 Provider（共享缓存）在装配时调用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CacheSerializerRegistration
{
    /// <summary>
    /// 已注册 <see cref="ICacheSerializer"/> 时不动；否则若已注册 <see cref="JsonSerializerOptions"/>，用它注册 <see cref="JsonCacheSerializer"/>；
    /// 两者都没有返回 <see langword="false"/>，由调用方决定报错或退回反射实现。
    /// </summary>
    public static bool TryAddJsonSerializer(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var hasSerializer = false;
        var hasOptions = false;
        foreach (var descriptor in services)
        {
            hasSerializer |= descriptor.ServiceType == typeof(ICacheSerializer);
            hasOptions |= descriptor.ServiceType == typeof(JsonSerializerOptions);
        }

        if (hasSerializer)
        {
            return true;
        }

        if (!hasOptions)
        {
            return false;
        }

        services.AddSingleton<ICacheSerializer>(static sp => new JsonCacheSerializer(sp.GetRequiredService<JsonSerializerOptions>()));
        return true;
    }
}
