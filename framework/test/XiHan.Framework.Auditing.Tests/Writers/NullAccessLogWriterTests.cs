// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Writers;

/// <summary>
/// 空访问日志写入器测试
/// </summary>
/// <remarks>
/// 空实现是「应用侧未接落库」时的默认占位，契约有三条：同步完成（不引入调度开销）、
/// 不修改传入记录（下游可能还要复用同一实例）、已取消令牌下也不抛（默认实现绝不成为故障源）。
/// </remarks>
public class NullAccessLogWriterTests
{
    /// <summary>
    /// 写入同步完成且不修改传入记录
    /// </summary>
    [Fact]
    public async Task WriteAsync_CompletesSynchronouslyWithoutTouchingRecord()
    {
        var writer = new NullAccessLogWriter();
        var record = new AccessLogRecord { TraceId = "trace-1", StatusCode = 200 };

        var task = writer.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        await task;

        Assert.Equal("trace-1", record.TraceId);
        Assert.Equal(200, record.StatusCode);
    }

    /// <summary>
    /// 令牌已取消时仍正常完成，不抛取消异常
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_StillCompletes()
    {
        var writer = new NullAccessLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await writer.WriteAsync(new AccessLogRecord(), cts.Token);
    }
}
