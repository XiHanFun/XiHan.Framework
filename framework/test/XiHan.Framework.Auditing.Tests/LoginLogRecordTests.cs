// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 登录日志记录模型测试
/// </summary>
/// <remarks>
/// <c>LoginResult</c> 是被查询与告警依赖的数值口径（0 = 成功），默认 0 意味着「未显式赋值即视为成功」，
/// 这是个容易踩的坑，所以默认值单独断言。<c>LoginTime</c> 是 <see cref="DateTimeOffset"/>，
/// 往返时必须保住偏移量，否则跨时区审计时间会漂。
/// </remarks>
public class LoginLogRecordTests
{
    /// <summary>
    /// 新建记录时全部标识字段为 null，登录结果为 0，登录时间为默认值
    /// </summary>
    [Fact]
    public void Ctor_Default_LeavesAllIdentityFieldsNull()
    {
        var record = new LoginLogRecord();

        Assert.Null(record.TraceId);
        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
        Assert.Null(record.SessionId);
        Assert.Null(record.Message);
        Assert.Null(record.LoginIp);
        Assert.Null(record.UserAgent);
        Assert.Null(record.DeviceId);

        Assert.Equal(0, record.LoginResult);
        Assert.Equal(default(DateTimeOffset), record.LoginTime);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变，含时区偏移
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesIncludingTimeZoneOffset()
    {
        var loginTime = new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.FromHours(8));
        var original = new LoginLogRecord
        {
            TraceId = "trace-4",
            UserId = 3,
            UserName = "tom",
            SessionId = "session-4",
            LoginResult = 1,
            Message = "密码错误",
            LoginIp = "10.0.0.4",
            UserAgent = "xunit",
            DeviceId = "device-4",
            LoginTime = loginTime
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<LoginLogRecord>(json);

        Assert.Contains("\"LoginResult\":", json);
        Assert.Contains("\"LoginTime\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.TraceId, restored!.TraceId);
        Assert.Equal(original.UserId, restored.UserId);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.LoginResult, restored.LoginResult);
        Assert.Equal(original.Message, restored.Message);
        Assert.Equal(original.DeviceId, restored.DeviceId);
        Assert.Equal(loginTime, restored.LoginTime);
        Assert.Equal(loginTime.Offset, restored.LoginTime.Offset);
    }
}
