// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Serialization;

/// <summary>
/// 序列化器的注册规则，供需要序列化的 Provider（共享缓存）在装配时调用
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CacheSerializerRegistration
{
    /// <summary>
    /// 已注册 <see cref="ICacheSerializer"/> 时不动
    /// </summary>
    /// <remarks>
    /// 否则若已注册 <see cref="JsonSerializerOptions"/>，用它注册 <see cref="JsonCacheSerializer"/>，组件经 <see cref="AddTypeInfo"/> 登记的类型元数据并在应用的解析器之后（ADR-0229）；两者都没有返回 <see langword="false"/>，由调用方决定报错或退回反射实现
    /// </remarks>
    public static bool TryAddJsonSerializer(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 只扫描描述符、不构建容器；优先级：显式 ICacheSerializer > 已注册的 JsonSerializerOptions > 交给调用方决定
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

        // 组件的登记在容器建好时才齐（特性装配顺序不定），所以在解析时合并
        services.AddSingleton<ICacheSerializer>(static sp => new JsonCacheSerializer(WithTypeInfo(sp.GetRequiredService<JsonSerializerOptions>(), sp.GetServices<CacheTypeInfoSource>())));
        return true;
    }

    /// <summary>
    /// 登记组件放进缓存的类型的序列化元数据
    /// </summary>
    /// <remarks>
    /// 框架组件把自己的源生成上下文登记在这里，应用的上下文不必认识框架的缓存类型；同一个解析器只登记一次。反射版序列化器本来就认识全部类型，应用自己登记 <see cref="ICacheSerializer"/> 时要自行处理这些类型
    /// </remarks>
    [ApiLevel(Stability.Preview, "5.0")]
    public static IServiceCollection AddTypeInfo(IServiceCollection services, IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(CacheTypeInfoSource) && descriptor.ImplementationInstance is CacheTypeInfoSource existing && ReferenceEquals(existing.Resolver, resolver))
            {
                return services;
            }
        }

        services.AddSingleton(new CacheTypeInfoSource(resolver));
        return services;
    }

    /// <summary>
    /// 把组件登记的类型元数据并到应用选项的解析器之后
    /// </summary>
    /// <remarks>
    /// 应用的解析器在前：同一类型两边都有时按应用的；应用选项没有解析器时原样返回，由 <see cref="JsonCacheSerializer"/> 报告配置错误
    /// </remarks>
    private static JsonSerializerOptions WithTypeInfo(JsonSerializerOptions options, IEnumerable<CacheTypeInfoSource> sources)
    {
        var resolvers = sources.Select(static source => source.Resolver).ToList();
        if (resolvers.Count == 0 || options.TypeInfoResolver is not { } application)
        {
            return options;
        }

        return new JsonSerializerOptions(options) { TypeInfoResolver = JsonTypeInfoResolver.Combine([application, .. resolvers]) };
    }
}
