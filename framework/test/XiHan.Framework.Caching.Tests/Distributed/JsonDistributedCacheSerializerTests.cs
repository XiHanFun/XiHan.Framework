// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 基于 System.Text.Json 的分布式缓存序列化器测试
/// </summary>
/// <remarks>
/// 缓存里的字节是跨进程、跨版本共享的，序列化器必须完全跟随注入的 JsonSerializerOptions，
/// 否则同一份配置下不同实例写出的键名会不一致，互相读不出对方的数据。
/// </remarks>
public class JsonDistributedCacheSerializerTests
{
    /// <summary>
    /// 序列化后可反序列化回等值对象
    /// </summary>
    [Fact]
    public void SerializeThenDeserialize_RoundTripsValue()
    {
        var serializer = CreateSerializer();

        var bytes = serializer.Serialize(new SampleCacheItem { Value = "曦寒" });

        Assert.Equal("曦寒", serializer.Deserialize<SampleCacheItem>(bytes).Value);
    }

    /// <summary>
    /// 值类型同样可往返
    /// </summary>
    [Fact]
    public void SerializeThenDeserialize_ForValueType_RoundTrips()
    {
        var serializer = CreateSerializer();

        Assert.Equal(42, serializer.Deserialize<int>(serializer.Serialize(42)));
    }

    /// <summary>
    /// 序列化输出为 UTF-8 字节
    /// </summary>
    [Fact]
    public void Serialize_ProducesUtf8Bytes()
    {
        var serializer = CreateSerializer();

        var json = Encoding.UTF8.GetString(serializer.Serialize(new SampleCacheItem { Value = "v" }));

        Assert.Equal("{\"Value\":\"v\"}", json);
    }

    /// <summary>
    /// 序列化跟随注入的命名策略
    /// </summary>
    [Fact]
    public void Serialize_FollowsInjectedNamingPolicy()
    {
        var serializer = CreateSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var json = Encoding.UTF8.GetString(serializer.Serialize(new SampleCacheItem { Value = "v" }));

        Assert.Equal("{\"value\":\"v\"}", json);
    }

    /// <summary>
    /// 空对象序列化为 JSON 空值
    /// </summary>
    [Fact]
    public void Serialize_ForNull_ProducesJsonNull()
    {
        var serializer = CreateSerializer();

        Assert.Equal("null", Encoding.UTF8.GetString(serializer.Serialize<SampleCacheItem?>(null)));
    }

    /// <summary>
    /// 反序列化得到空值时抛出可定位类型的异常
    /// </summary>
    /// <remarks>
    /// 缓存值为空与缓存未命中在上层是两种语义，这里必须显式失败而不是悄悄返回 null。
    /// </remarks>
    [Fact]
    public void Deserialize_ForJsonNull_ThrowsWithTypeName()
    {
        var serializer = CreateSerializer();

        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.Deserialize<SampleCacheItem>(Encoding.UTF8.GetBytes("null")));

        Assert.Contains(typeof(SampleCacheItem).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 反序列化损坏内容时抛出 JSON 异常
    /// </summary>
    [Fact]
    public void Deserialize_ForCorruptedContent_ThrowsJsonException()
    {
        var serializer = CreateSerializer();

        Assert.ThrowsAny<JsonException>(
            () => serializer.Deserialize<SampleCacheItem>(Encoding.UTF8.GetBytes("{ not json")));
    }

    /// <summary>
    /// 反序列化跟随注入的命名策略
    /// </summary>
    [Fact]
    public void Deserialize_FollowsInjectedNamingPolicy()
    {
        var serializer = CreateSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var item = serializer.Deserialize<SampleCacheItem>(Encoding.UTF8.GetBytes("{\"value\":\"v\"}"));

        Assert.Equal("v", item.Value);
    }

    /// <summary>
    /// 创建序列化器
    /// </summary>
    /// <param name="options">序列化选项</param>
    /// <returns>序列化器</returns>
    private static JsonDistributedCacheSerializer CreateSerializer(JsonSerializerOptions? options = null)
    {
        return new JsonDistributedCacheSerializer(
            Microsoft.Extensions.Options.Options.Create(options ?? new JsonSerializerOptions()));
    }
}
