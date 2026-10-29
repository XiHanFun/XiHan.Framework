// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 写入序号。
/// </summary>
/// <remarks>
/// 同刻写入的行按它排序。取时钟的 UTC 刻度与上一个序号加一的较大者——同一进程内严格递增，不同进程间按时刻大致有序；同一实例的写入由实例锁串行化，所以同一实例的行按它排就是写入顺序。UUID v7 只到毫秒单调，同一毫秒内的 ID 不能当写入顺序用。
/// </remarks>
internal sealed class WriteSequence
{
    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 上一个发出的序号。
    /// </summary>
    private long _last;

    /// <summary>
    /// 创建。
    /// </summary>
    public WriteSequence(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 下一个序号。
    /// </summary>
    public long Next()
    {
        var now = _clock.GetUtcNow().UtcTicks;
        while (true)
        {
            // 无锁递增：时钟回拨或同一刻度内多次取号时退化为上一个加一，保证严格递增
            var last = Volatile.Read(ref _last);
            var next = Math.Max(last + 1, now);
            if (Interlocked.CompareExchange(ref _last, next, last) == last)
            {
                return next;
            }
        }
    }
}
