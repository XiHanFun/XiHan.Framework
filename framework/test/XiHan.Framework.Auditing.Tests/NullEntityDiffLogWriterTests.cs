// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 空实体差异日志写入器测试
/// </summary>
public class NullEntityDiffLogWriterTests
{
    /// <summary>
    /// 写入同步完成且不修改传入记录
    /// </summary>
    [Fact]
    public async Task WriteAsync_CompletesSynchronouslyWithoutTouchingRecord()
    {
        var writer = new NullEntityDiffLogWriter();
        var record = new EntityDiffLogRecord
        {
            OperationType = "Update",
            EntityType = "SampleDomain.Entities.SampleOrder",
            BeforeData = "{\"Amount\":1}",
            AfterData = "{\"Amount\":2}"
        };

        var task = writer.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        await task;

        Assert.Equal("EntityChange", record.AuditType);
        Assert.Equal("Update", record.OperationType);
        Assert.Equal("{\"Amount\":1}", record.BeforeData);
        Assert.Equal("{\"Amount\":2}", record.AfterData);
    }

    /// <summary>
    /// 令牌已取消时仍正常完成，不抛取消异常
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_StillCompletes()
    {
        var writer = new NullEntityDiffLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await writer.WriteAsync(new EntityDiffLogRecord(), cts.Token);
    }
}
