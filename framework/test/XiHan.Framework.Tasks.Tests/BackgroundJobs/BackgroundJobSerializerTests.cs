// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 后台作业参数序列化器测试
/// </summary>
/// <remarks>
/// 序列化结果会直接落库，反序列化失败在 Worker 侧被当作"致命错误"直接放弃作业，
/// 所以这里既要验证往返一致，也要验证失败时抛的是可识别的异常而不是返回 null 让上游继续用。
/// </remarks>
public class BackgroundJobSerializerTests
{
    /// <summary>
    /// 序列化后再反序列化保持字段值
    /// </summary>
    [Fact]
    public void SerializeThenDeserialize_PreservesValues()
    {
        var serializer = new BackgroundJobSerializer();
        var args = new NamedJobArgs { Value = "订单已创建", Count = 7 };

        var json = serializer.Serialize(args);
        var restored = Assert.IsType<NamedJobArgs>(serializer.Deserialize(json, typeof(NamedJobArgs)));

        Assert.Equal("订单已创建", restored.Value);
        Assert.Equal(7, restored.Count);
    }

    /// <summary>
    /// 序列化输出为紧凑单行 JSON，避免把无意义的缩进写进存储
    /// </summary>
    [Fact]
    public void Serialize_ProducesCompactJson()
    {
        var serializer = new BackgroundJobSerializer();

        var json = serializer.Serialize(new NamedJobArgs { Value = "x", Count = 1 });

        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.Equal("""{"Value":"x","Count":1}""", json);
    }

    /// <summary>
    /// 反序列化得到 null 时抛出可识别的异常并带上目标类型
    /// </summary>
    [Fact]
    public void Deserialize_WhenPayloadIsNullLiteral_ThrowsInvalidOperationException()
    {
        var serializer = new BackgroundJobSerializer();

        var exception = Assert.Throws<InvalidOperationException>(() => serializer.Deserialize("null", typeof(NamedJobArgs)));

        Assert.Contains(typeof(NamedJobArgs).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 载荷不是合法 JSON 时抛出 JSON 异常，由 Worker 归入致命错误
    /// </summary>
    [Fact]
    public void Deserialize_WhenPayloadIsInvalid_ThrowsJsonException()
    {
        var serializer = new BackgroundJobSerializer();

        Assert.Throws<JsonException>(() => serializer.Deserialize("{ not json", typeof(NamedJobArgs)));
    }

    /// <summary>
    /// 载荷字段与目标类型不匹配时抛出 JSON 异常
    /// </summary>
    [Fact]
    public void Deserialize_WhenFieldTypeMismatch_ThrowsJsonException()
    {
        var serializer = new BackgroundJobSerializer();

        Assert.Throws<JsonException>(() => serializer.Deserialize("""{"Count":"not-a-number"}""", typeof(NamedJobArgs)));
    }

    /// <summary>
    /// 序列化按运行时类型而非声明类型输出，装箱成 object 不会丢字段
    /// </summary>
    [Fact]
    public void Serialize_UsesRuntimeType()
    {
        var serializer = new BackgroundJobSerializer();
        object args = new NamedJobArgs { Value = "y", Count = 2 };

        var json = serializer.Serialize(args);

        Assert.Contains("\"Value\":\"y\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Count\":2", json, StringComparison.Ordinal);
    }
}
