// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存Json序列化器工厂
/// </summary>
public class XiHanHybridCacheJsonSerializerFactory : IHybridCacheSerializerFactory
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    public XiHanHybridCacheJsonSerializerFactory(IOptions<JsonSerializerOptions> options)
    {
        Options = options;
    }

    /// <summary>
    /// System.Text.Json序列化器选项
    /// </summary>
    protected IOptions<JsonSerializerOptions> Options { get; }

    /// <summary>
    /// 尝试创建序列化器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="serializer"></param>
    /// <returns></returns>
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        if (typeof(T) == typeof(string) || typeof(T) == typeof(byte[]))
        {
            serializer = null;
            return false;
        }

        serializer = new XiHanHybridCacheJsonSerializer<T>(Options.Value);
        return true;
    }
}
