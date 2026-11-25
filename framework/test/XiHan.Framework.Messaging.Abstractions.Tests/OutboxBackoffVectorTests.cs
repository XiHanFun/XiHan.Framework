// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 回放 <c>outbox-backoff.json</c>：发件箱失败之后的退避间隔与进死信的时机
/// </summary>
/// <remarks>
/// <c>messaging/outbox-backoff.json</c>（RFC-0009「发件箱」，ADR-0064、ADR-0108、ADR-0277、ADR-0283）。间隔经 <see cref="OutboxOptions.RetryDelayAfter"/> 回放，再经 <see cref="OutboxDispatcher"/> 走一遍：一行已经失败过若干次、这一次又失败，看它被推后多久、有没有进死信。投递循环用按脚本应答的工作单元与仓储驱动，行就是内存里的那个对象
/// </remarks>
public sealed class OutboxBackoffVectorTests
{
    /// <summary>
    /// 投递时的时刻
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly BackoffVectors _vectors = SpecVectors.Read<BackoffVectors>("messaging", "outbox-backoff.json");

    [Fact]
    public void Defaults_AreTheOnesInTheVectors()
    {
        var options = new OutboxOptions();

        Assert.Equal(TimeSpan.FromMilliseconds(_vectors.Defaults.RetryDelayMs), options.RetryDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(_vectors.Defaults.MaxRetryDelayMs), options.MaxRetryDelay);
        Assert.Equal(_vectors.Defaults.MaxAttempts, options.MaxAttempts);
    }

    [Fact]
    public void RetryDelayAfter_IsTheDelayOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var actual = Options(vector).RetryDelayAfter(vector.Attempts);
            if (actual != TimeSpan.FromMilliseconds(vector.DelayMs))
            {
                failures.Add($"{vector.Name}: the delay is {actual.TotalMilliseconds} ms, the vector has {vector.DelayMs} ms");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Dispatcher_PostponesAFailedMessage_ByTheDelayOfEveryCase()
    {
        var failures = new List<string>();
        var replayed = 0;
        foreach (var vector in _vectors.Cases)
        {
            // 尝试上限取 32 位整数的上限，行才不会在这里进死信；失败次数正好到这个上限的那一条因此只能直接算，不经投递循环
            if (vector.Attempts == int.MaxValue)
            {
                continue;
            }

            replayed++;
            var row = await FailOnceAsync(Options(vector) with { MaxAttempts = int.MaxValue }, vector.Attempts);
            if (row.Attempts != vector.Attempts || row.DeadLetteredAt is not null || row.NextAttemptAt != _now.AddMilliseconds(vector.DelayMs))
            {
                failures.Add($"{vector.Name}: after attempt {row.Attempts} the next attempt is due {(row.NextAttemptAt - _now)?.TotalMilliseconds} ms later (dead-lettered: {row.DeadLetteredAt is not null}), the vector has {vector.DelayMs} ms");
            }
        }

        Assert.True(replayed > 0, "No case of the vectors could be replayed through the dispatcher.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Dispatcher_DeadLettersAMessage_WhenItsAttemptsReachTheMaximum()
    {
        Assert.NotEmpty(_vectors.DeadLetter);
        var failures = new List<string>();
        foreach (var vector in _vectors.DeadLetter)
        {
            var options = new OutboxOptions { MaxAttempts = vector.MaxAttempts };
            var row = await FailOnceAsync(options, vector.Attempts);

            // 进死信的记下时刻、不再排下一次；没进的按退避排下一次
            var deadLettered = row.DeadLetteredAt == _now && row.NextAttemptAt is null;
            var postponed = row.DeadLetteredAt is null && row.NextAttemptAt == _now + options.RetryDelayAfter(vector.Attempts);
            if (row.Attempts != vector.Attempts || !(vector.DeadLettered ? deadLettered : postponed))
            {
                failures.Add($"maxAttempts {vector.MaxAttempts}, attempts {vector.Attempts}: the row has {row.Attempts} attempts, dead-lettered at '{row.DeadLetteredAt}', next attempt at '{row.NextAttemptAt}'; the vector has deadLettered = {vector.DeadLettered}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheCap_TheOverflow_AndBothSidesOfTheMaximum()
    {
        // 防止向量换了写法后边界一条都没核对还照样通过：没到上限的、正好等于上限的、被上限截住的各要有
        Assert.Contains(_vectors.Cases, static vector => vector.DelayMs < vector.MaxRetryDelayMs);
        Assert.Contains(_vectors.Cases, static vector => vector.DelayMs == vector.MaxRetryDelayMs && Uncapped(vector) == vector.MaxRetryDelayMs);
        Assert.Contains(_vectors.Cases, static vector => vector.DelayMs == vector.MaxRetryDelayMs && Uncapped(vector) > vector.MaxRetryDelayMs);

        // 指数会溢出 64 位整数的失败次数也要有：实现不得因此得出负数、零或别的值
        Assert.Contains(_vectors.Cases, static vector => vector.Attempts > 64);
        Assert.Contains(_vectors.Cases, static vector => vector.Attempts == int.MaxValue);

        // 上限是间隔的 2 的 30 次方倍以上：间隔要越过这个倍数，再饱和在上限，不能把指数截在某个值上（ADR-0283）
        Assert.Contains(_vectors.Cases, static vector => vector.DelayMs > vector.RetryDelayMs << 30 && vector.DelayMs < vector.MaxRetryDelayMs);
        Assert.Contains(_vectors.Cases, static vector => vector.MaxRetryDelayMs > vector.RetryDelayMs << 30 && vector.DelayMs == vector.MaxRetryDelayMs);

        // 进死信的时机：差一次到上限的不进，正好到上限的进，超过上限的也进
        Assert.Contains(_vectors.DeadLetter, static vector => vector.Attempts == vector.MaxAttempts - 1 && !vector.DeadLettered);
        Assert.Contains(_vectors.DeadLetter, static vector => vector.Attempts == vector.MaxAttempts && vector.DeadLettered);
        Assert.Contains(_vectors.DeadLetter, static vector => vector.Attempts > vector.MaxAttempts && vector.DeadLettered);
    }

    /// <summary>
    /// 一个用例的两个选项
    /// </summary>
    /// <param name="vector">用例</param>
    private static OutboxOptions Options(BackoffCase vector)
        => new() { RetryDelay = TimeSpan.FromMilliseconds(vector.RetryDelayMs), MaxRetryDelay = TimeSpan.FromMilliseconds(vector.MaxRetryDelayMs) };

    /// <summary>
    /// 不设上限时这次失败之后的间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 正文的公式：间隔乘以 2 的（失败次数减一）次方。只用来给用例分类，用浮点数算，次数再多也只是无穷大
    /// </remarks>
    /// <param name="vector">用例</param>
    private static double Uncapped(BackoffCase vector) => vector.RetryDelayMs * Math.Pow(2, vector.Attempts - 1);

    /// <summary>
    /// 让一条已经失败过若干次的消息再失败一次，返回投递循环写回的行
    /// </summary>
    /// <param name="options">发件箱选项</param>
    /// <param name="attempts">这次失败之后的尝试次数（从 1 起）</param>
    private static async Task<OutboxMessage> FailOnceAsync(OutboxOptions options, int attempts)
    {
        // 行在这次投递之前已经尝试过 attempts - 1 次，到了投递时间、没有进死信
        var row = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Topic = "orders",
            Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = "1", Source = "/orders", Type = "orders.placed" }),
            CreatedAt = _now.AddHours(-1),
            Attempts = attempts - 1,
        };
        await using var services = new ServiceCollection()
            .AddScoped<IUnitOfWork>(static _ => new ScriptedUnitOfWork())
            .AddScoped<IRepository<OutboxMessage, Guid>>(_ => new ScriptedRepository<OutboxMessage, Guid> { Found = row })
            .BuildServiceProvider();
        var broker = new FixedOutcomeBroker(Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", "nacked"))));
        var dispatcher = new OutboxDispatcher(services, broker, options, new FixedClock(_now), NullLoggerFactory.Instance);

        // 代理返回失败：这一轮没有投递成功的，行记一次尝试
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["orders"], broker.Topics);
        return row;
    }

    /// <summary>
    /// 停在一个时刻的时钟
    /// </summary>
    /// <param name="now">当前时刻</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Defaults">三个选项的缺省值</param>
    /// <param name="Cases">退避间隔的用例</param>
    /// <param name="DeadLetter">进死信时机的用例</param>
    private sealed record BackoffVectors(BackoffDefaults Defaults, BackoffCase[] Cases, DeadLetterCase[] DeadLetter);

    /// <summary>
    /// 三个选项的缺省值
    /// </summary>
    /// <param name="RetryDelayMs">第一次失败后的重试间隔（毫秒）</param>
    /// <param name="MaxRetryDelayMs">重试间隔的上限（毫秒）</param>
    /// <param name="MaxAttempts">最多尝试的次数</param>
    private sealed record BackoffDefaults(long RetryDelayMs, long MaxRetryDelayMs, int MaxAttempts);

    /// <summary>
    /// 一个退避间隔的用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="RetryDelayMs">第一次失败后的重试间隔（毫秒）</param>
    /// <param name="MaxRetryDelayMs">重试间隔的上限（毫秒）</param>
    /// <param name="Attempts">已经失败的尝试次数（从 1 起）</param>
    /// <param name="DelayMs">这次失败之后推后的间隔（毫秒）</param>
    private sealed record BackoffCase(string Name, long RetryDelayMs, long MaxRetryDelayMs, int Attempts, long DelayMs);

    /// <summary>
    /// 一个进死信时机的用例
    /// </summary>
    /// <param name="MaxAttempts">最多尝试的次数</param>
    /// <param name="Attempts">这次失败之后的尝试次数</param>
    /// <param name="DeadLettered">是否进死信</param>
    private sealed record DeadLetterCase(int MaxAttempts, int Attempts, bool DeadLettered);
}
