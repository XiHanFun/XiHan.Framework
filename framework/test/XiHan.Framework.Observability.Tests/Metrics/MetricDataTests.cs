// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Observability.Metrics;

namespace XiHan.Framework.Observability.Tests.Metrics;

/// <summary>
/// 指标数据模型测试
/// </summary>
/// <remarks>
/// MetricData 是对外暴露/可序列化的契约模型，因此锁三件事：默认值语义、MetricType 的数值、System.Text.Json 往返一致。
/// MetricType 会被序列化为数字落到导出侧，数值一旦漂移历史数据即错位，必须逐个钉死。
/// </remarks>
public class MetricDataTests
{
    /// <summary>
    /// 新建实例的默认值：字符串空、集合非空、时间戳取当前 UTC
    /// </summary>
    [Fact]
    public void Constructor_Default_InitializesSafeDefaults()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var data = new MetricData();

        Assert.Equal(string.Empty, data.Name);
        Assert.Equal(MetricType.Counter, data.Type);
        Assert.Equal(0d, data.Value);
        Assert.NotNull(data.Tags);
        Assert.Empty(data.Tags);
        Assert.Null(data.Unit);
        Assert.InRange(data.Timestamp, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    /// <summary>
    /// 每个实例持有独立的标签字典，互不共享
    /// </summary>
    [Fact]
    public void Tags_OnTwoInstances_AreIndependentDictionaries()
    {
        var first = new MetricData();
        var second = new MetricData();

        first.Tags["env"] = "dev";

        Assert.NotSame(first.Tags, second.Tags);
        Assert.Empty(second.Tags);
    }

    /// <summary>
    /// MetricData 是普通类，走引用相等而非值相等
    /// </summary>
    [Fact]
    public void Equals_TwoInstancesWithSameContent_UsesReferenceSemantics()
    {
        var first = new MetricData { Name = "a", Value = 1d };
        var second = new MetricData { Name = "a", Value = 1d };

        Assert.NotEqual(first, second);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 全部属性可写，赋值后原样读回
    /// </summary>
    [Fact]
    public void Properties_WhenAssigned_AreReadBackVerbatim()
    {
        var timestamp = new DateTimeOffset(2024, 5, 1, 12, 30, 0, TimeSpan.Zero);

        var data = new MetricData
        {
            Name = "http.server.duration",
            Type = MetricType.Histogram,
            Value = 12.5d,
            Unit = "ms",
            Timestamp = timestamp,
            Tags = new Dictionary<string, string> { ["route"] = "/api/values" }
        };

        Assert.Equal("http.server.duration", data.Name);
        Assert.Equal(MetricType.Histogram, data.Type);
        Assert.Equal(12.5d, data.Value);
        Assert.Equal("ms", data.Unit);
        Assert.Equal(timestamp, data.Timestamp);
        Assert.Equal("/api/values", data.Tags["route"]);
    }

    /// <summary>
    /// 指标类型枚举的数值被导出侧依赖，不允许调整顺序
    /// </summary>
    [Theory]
    [InlineData(MetricType.Counter, 0)]
    [InlineData(MetricType.Gauge, 1)]
    [InlineData(MetricType.Histogram, 2)]
    [InlineData(MetricType.Summary, 3)]
    public void MetricType_UnderlyingValues_AreStable(MetricType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    /// <summary>
    /// 指标类型仅有四个成员，新增成员必须显式评审
    /// </summary>
    [Fact]
    public void MetricType_MemberCount_IsFour()
    {
        Assert.Equal(4, Enum.GetValues<MetricType>().Length);
    }

    /// <summary>
    /// System.Text.Json 默认配置下往返一致，字段名保持 Pascal 命名
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithDefaultOptions_PreservesAllFields()
    {
        var original = new MetricData
        {
            Name = "queue.depth",
            Type = MetricType.Gauge,
            Value = 7d,
            Unit = "items",
            Timestamp = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Tags = new Dictionary<string, string> { ["queue"] = "orders" }
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<MetricData>(json);

        Assert.Contains("\"Name\"", json);
        Assert.Contains("\"Timestamp\"", json);
        Assert.NotNull(restored);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.Value, restored.Value);
        Assert.Equal(original.Unit, restored.Unit);
        Assert.Equal(original.Timestamp, restored.Timestamp);
        Assert.Equal("orders", restored.Tags["queue"]);
    }

    /// <summary>
    /// 单位为 null 时序列化后仍能还原为 null
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithNullUnit_KeepsNull()
    {
        var json = JsonSerializer.Serialize(new MetricData { Name = "n" });

        var restored = JsonSerializer.Deserialize<MetricData>(json);

        Assert.NotNull(restored);
        Assert.Null(restored.Unit);
        Assert.Equal("n", restored.Name);
    }
}
