// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Web.Core.Clients;

namespace XiHan.Framework.Web.Core.Tests.Clients;

/// <summary>
/// 客户端信息数据契约测试
/// </summary>
/// <remarks>
/// 这个类型会被审计日志、登录日志直接落库并回传前端，
/// 因此锁两件事：六个字段全部可空（解析不出来就是 null，不能给假值），以及 JSON 字段名不漂移。
/// </remarks>
public class ClientInfoTests
{
    /// <summary>
    /// 新建实例的所有字段默认为空，未解析出的信息不得被填成空串
    /// </summary>
    [Fact]
    public void Defaults_AllFieldsAreNull()
    {
        var info = new ClientInfo();

        Assert.Null(info.IpAddress);
        Assert.Null(info.Location);
        Assert.Null(info.UserAgent);
        Assert.Null(info.Browser);
        Assert.Null(info.OperatingSystem);
        Assert.Null(info.DeviceName);
    }

    /// <summary>
    /// JSON 序列化字段名与属性名一致，往返后各字段值不丢失
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsPropertyNamesAndValues()
    {
        var info = new ClientInfo
        {
            IpAddress = "203.0.113.7",
            Location = "中国 广东 深圳",
            UserAgent = "XiHanTestAgent",
            Browser = "Chrome 120.0.0",
            OperatingSystem = "Windows 10",
            DeviceName = "PC"
        };

        var json = JsonSerializer.Serialize(info);

        Assert.Contains("\"IpAddress\":\"203.0.113.7\"", json, StringComparison.Ordinal);
        Assert.Contains("\"UserAgent\":\"XiHanTestAgent\"", json, StringComparison.Ordinal);
        Assert.Contains("\"OperatingSystem\":", json, StringComparison.Ordinal);
        Assert.Contains("\"DeviceName\":\"PC\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<ClientInfo>(json);

        Assert.NotNull(restored);
        Assert.Equal(info.IpAddress, restored.IpAddress);
        Assert.Equal(info.Location, restored.Location);
        Assert.Equal(info.UserAgent, restored.UserAgent);
        Assert.Equal(info.Browser, restored.Browser);
        Assert.Equal(info.OperatingSystem, restored.OperatingSystem);
        Assert.Equal(info.DeviceName, restored.DeviceName);
    }

    /// <summary>
    /// 空实例序列化后各字段仍以 null 出现，反序列化回来不会被填成空串
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WhenAllFieldsNull_StaysNull()
    {
        var json = JsonSerializer.Serialize(new ClientInfo());

        var restored = JsonSerializer.Deserialize<ClientInfo>(json);

        Assert.NotNull(restored);
        Assert.Null(restored.IpAddress);
        Assert.Null(restored.Location);
        Assert.Null(restored.UserAgent);
        Assert.Null(restored.Browser);
        Assert.Null(restored.OperatingSystem);
        Assert.Null(restored.DeviceName);
    }
}
