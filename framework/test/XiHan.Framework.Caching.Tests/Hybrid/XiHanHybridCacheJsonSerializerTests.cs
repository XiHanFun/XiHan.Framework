// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Tests.Fakes;

namespace XiHan.Framework.Caching.Tests.Hybrid;

/// <summary>
/// 混合缓存 Json 序列化器与其工厂测试
/// </summary>
/// <remarks>
/// 工厂对字符串与字节数组显式让位，交给运行时内建的直通序列化器；
/// 这条判定一旦丢失，字符串会被包成 JSON 字面量多带一对引号，与内建实现写出的字节不兼容。
/// </remarks>
public class XiHanHybridCacheJsonSerializerTests
{
    /// <summary>
    /// 序列化后可反序列化回等值对象
    /// </summary>
    [Fact]
    public void SerializeThenDeserialize_RoundTripsValue()
    {
        var serializer = new XiHanHybridCacheJsonSerializer<SampleCacheItem>(new JsonSerializerOptions());
        var writer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new SampleCacheItem { Value = "曦寒" }, writer);
        var item = serializer.Deserialize(new ReadOnlySequence<byte>(writer.WrittenMemory));

        Assert.Equal("曦寒", item.Value);
    }

    /// <summary>
    /// 序列化跟随注入的命名策略
    /// </summary>
    [Fact]
    public void Serialize_FollowsInjectedNamingPolicy()
    {
        var serializer = new XiHanHybridCacheJsonSerializer<SampleCacheItem>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var writer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new SampleCacheItem { Value = "v" }, writer);

        Assert.Equal("{\"value\":\"v\"}", Encoding.UTF8.GetString(writer.WrittenSpan));
    }

    /// <summary>
    /// 工厂为普通对象类型创建 Json 序列化器
    /// </summary>
    [Fact]
    public void TryCreateSerializer_ForObjectType_ReturnsJsonSerializer()
    {
        var factory = CreateFactory();

        Assert.True(factory.TryCreateSerializer<SampleCacheItem>(out var serializer));
        Assert.IsType<XiHanHybridCacheJsonSerializer<SampleCacheItem>>(serializer);
    }

    /// <summary>
    /// 工厂对字符串让位给内建序列化器
    /// </summary>
    [Fact]
    public void TryCreateSerializer_ForString_DeclinesAndReturnsNull()
    {
        var factory = CreateFactory();

        Assert.False(factory.TryCreateSerializer<string>(out var serializer));
        Assert.Null(serializer);
    }

    /// <summary>
    /// 工厂对字节数组让位给内建序列化器
    /// </summary>
    [Fact]
    public void TryCreateSerializer_ForByteArray_DeclinesAndReturnsNull()
    {
        var factory = CreateFactory();

        Assert.False(factory.TryCreateSerializer<byte[]>(out var serializer));
        Assert.Null(serializer);
    }

    /// <summary>
    /// 工厂创建出来的序列化器沿用工厂持有的选项
    /// </summary>
    [Fact]
    public void TryCreateSerializer_UsesFactoryOptions()
    {
        var factory = new XiHanHybridCacheJsonSerializerFactory(
            Microsoft.Extensions.Options.Options.Create(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        var writer = new ArrayBufferWriter<byte>();

        var created = factory.TryCreateSerializer<SampleCacheItem>(out var serializer);

        Assert.True(created);
        Assert.NotNull(serializer);
        serializer.Serialize(new SampleCacheItem { Value = "v" }, writer);
        Assert.Equal("{\"value\":\"v\"}", Encoding.UTF8.GetString(writer.WrittenSpan));
    }

    /// <summary>
    /// 创建序列化器工厂
    /// </summary>
    /// <returns>工厂</returns>
    private static XiHanHybridCacheJsonSerializerFactory CreateFactory()
    {
        return new XiHanHybridCacheJsonSerializerFactory(
            Microsoft.Extensions.Options.Options.Create(new JsonSerializerOptions()));
    }
}
