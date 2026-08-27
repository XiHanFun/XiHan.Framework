// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Writers;

/// <summary>
/// 空登录日志写入器测试
/// </summary>
public class NullLoginLogWriterTests
{
    /// <summary>
    /// 写入同步完成且不修改传入记录
    /// </summary>
    [Fact]
    public async Task WriteAsync_CompletesSynchronouslyWithoutTouchingRecord()
    {
        var writer = new NullLoginLogWriter();
        var loginTime = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);
        var record = new LoginLogRecord { TraceId = "trace-1", LoginResult = 1, LoginTime = loginTime };

        var task = writer.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        await task;

        Assert.Equal("trace-1", record.TraceId);
        Assert.Equal(1, record.LoginResult);
        Assert.Equal(loginTime, record.LoginTime);
    }

    /// <summary>
    /// 令牌已取消时仍正常完成，不抛取消异常
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_StillCompletes()
    {
        var writer = new NullLoginLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await writer.WriteAsync(new LoginLogRecord(), cts.Token);
    }
}
