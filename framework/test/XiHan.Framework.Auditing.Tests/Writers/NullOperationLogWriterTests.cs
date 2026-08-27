// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Writers;

/// <summary>
/// 空操作日志写入器测试
/// </summary>
public class NullOperationLogWriterTests
{
    /// <summary>
    /// 写入同步完成且不修改传入记录
    /// </summary>
    [Fact]
    public async Task WriteAsync_CompletesSynchronouslyWithoutTouchingRecord()
    {
        var writer = new NullOperationLogWriter();
        var record = new OperationLogRecord { TraceId = "trace-1", StatusCode = 204 };

        var task = writer.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        await task;

        Assert.Equal("trace-1", record.TraceId);
        Assert.Equal(204, record.StatusCode);
    }

    /// <summary>
    /// 令牌已取消时仍正常完成，不抛取消异常
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_StillCompletes()
    {
        var writer = new NullOperationLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await writer.WriteAsync(new OperationLogRecord(), cts.Token);
    }
}
