// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Models;

/// <summary>
/// 后台作业持久化信息测试
/// </summary>
/// <remarks>
/// 这是存储边界上的传输模型：Redis 存储直接把它序列化成 JSON 落库，
/// 所以字段名、可空性与默认值都是对外契约——改了会让升级前入库的作业读不回来。
/// </remarks>
public class BackgroundJobInfoTests
{
    /// <summary>
    /// 新建实例的默认语义：未尝试、未放弃、普通优先级、无上次尝试时间
    /// </summary>
    [Fact]
    public void Defaults_AreNotTriedAndNotAbandoned()
    {
        var info = new BackgroundJobInfo();

        Assert.Equal(Guid.Empty, info.Id);
        Assert.Null(info.ApplicationName);
        Assert.Null(info.TenantId);
        Assert.Equal((short)0, info.TryCount);
        Assert.Null(info.LastTryTime);
        Assert.False(info.IsAbandoned);
        Assert.Equal(BackgroundJobPriority.Normal, info.Priority);
    }

    /// <summary>
    /// 尝试次数使用 short，能容纳长期退避重试的累计次数且不会被静默截断
    /// </summary>
    [Fact]
    public void TryCount_IsShortAndAcceptsLargeValues()
    {
        var info = new BackgroundJobInfo { TryCount = short.MaxValue };

        Assert.Equal(short.MaxValue, info.TryCount);
    }

    /// <summary>
    /// JSON 往返保持全部字段，且属性名保持 Pascal 命名
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var info = new BackgroundJobInfo
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ApplicationName = "order-service",
            TenantId = 42,
            JobName = "order-created",
            JobArgs = "{\"Value\":\"x\"}",
            TryCount = 3,
            CreationTime = created,
            NextTryTime = created.AddMinutes(1),
            LastTryTime = created.AddSeconds(30),
            IsAbandoned = true,
            Priority = BackgroundJobPriority.High
        };

        var json = JsonSerializer.Serialize(info);
        var restored = JsonSerializer.Deserialize<BackgroundJobInfo>(json);

        Assert.Contains("\"JobName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"NextTryTime\"", json, StringComparison.Ordinal);
        Assert.NotNull(restored);
        Assert.Equal(info.Id, restored.Id);
        Assert.Equal(info.ApplicationName, restored.ApplicationName);
        Assert.Equal(info.TenantId, restored.TenantId);
        Assert.Equal(info.JobName, restored.JobName);
        Assert.Equal(info.JobArgs, restored.JobArgs);
        Assert.Equal(info.TryCount, restored.TryCount);
        Assert.Equal(info.CreationTime, restored.CreationTime);
        Assert.Equal(info.NextTryTime, restored.NextTryTime);
        Assert.Equal(info.LastTryTime, restored.LastTryTime);
        Assert.True(restored.IsAbandoned);
        Assert.Equal(BackgroundJobPriority.High, restored.Priority);
    }

    /// <summary>
    /// 优先级按数值序列化，改成字符串会破坏已入库数据的读取
    /// </summary>
    [Fact]
    public void JsonSerialize_WritesPriorityAsNumber()
    {
        var json = JsonSerializer.Serialize(new BackgroundJobInfo { Priority = BackgroundJobPriority.AboveNormal });

        Assert.Contains("\"Priority\":20", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// 可空字段缺省时能正常反序列化回 null
    /// </summary>
    [Fact]
    public void JsonDeserialize_WhenOptionalFieldsMissing_KeepsNulls()
    {
        const string Payload = """{"Id":"11111111-2222-3333-4444-555555555555","JobName":"n","JobArgs":"{}"}""";

        var restored = JsonSerializer.Deserialize<BackgroundJobInfo>(Payload);

        Assert.NotNull(restored);
        Assert.Null(restored.ApplicationName);
        Assert.Null(restored.TenantId);
        Assert.Null(restored.LastTryTime);
    }
}
