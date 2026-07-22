// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using System.Buffers;
using System.Text.Json;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存Json序列化器
/// </summary>
public class XiHanHybridCacheJsonSerializer<T> : IHybridCacheSerializer<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jsonSerializerOptions"></param>
    public XiHanHybridCacheJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
    {
        JsonSerializerOptions = jsonSerializerOptions;
    }

    /// <summary>
    /// Json序列化选项
    /// </summary>
    protected JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public virtual T Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize<T>(ref reader, JsonSerializerOptions)!;
    }

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="value"></param>
    /// <param name="target"></param>
    public virtual void Serialize(T value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, JsonSerializerOptions);
    }
}
