// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Writers;

/// <summary>
/// 空接口日志写入器测试
/// </summary>
public class NullApiLogWriterTests
{
    /// <summary>
    /// 写入同步完成且不修改传入记录（含两个乐观布尔默认值）
    /// </summary>
    [Fact]
    public async Task WriteAsync_CompletesSynchronouslyWithoutTouchingRecord()
    {
        var writer = new NullApiLogWriter();
        var record = new ApiLogRecord { TraceId = "trace-1", IsSuccess = false };

        var task = writer.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        await task;

        Assert.Equal("trace-1", record.TraceId);
        Assert.False(record.IsSuccess);
        Assert.True(record.IsSignatureValid);
    }

    /// <summary>
    /// 令牌已取消时仍正常完成，不抛取消异常
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_StillCompletes()
    {
        var writer = new NullApiLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await writer.WriteAsync(new ApiLogRecord(), cts.Token);
    }
}
