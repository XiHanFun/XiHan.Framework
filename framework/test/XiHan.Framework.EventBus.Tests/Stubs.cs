// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 测试桩：租户上下文
/// </summary>
/// <remarks>
/// 收件箱写入路径不读取租户上下文，仅为满足基类构造函数的非空校验。
/// </remarks>
public sealed class StubCurrentTenant : ICurrentTenant
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public long? Id => null;

    /// <inheritdoc />
    public string? Name => null;

    /// <inheritdoc />
    public IDisposable Change(long? id, string? name = null) => new StubDisposable();
}

/// <summary>
/// 测试桩：工作单元管理器
/// </summary>
/// <remarks>
/// 收件箱写入路径不触碰工作单元，任何调用都视为测试用法错误。
/// </remarks>
public sealed class StubUnitOfWorkManager : IUnitOfWorkManager
{
    /// <inheritdoc />
    public IUnitOfWork? Current => null;

    /// <inheritdoc />
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false) => throw new NotSupportedException();

    /// <inheritdoc />
    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false) => throw new NotSupportedException();

    /// <inheritdoc />
    public void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();

    /// <inheritdoc />
    public bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();
}

/// <summary>
/// 测试桩：事件处理器调用器
/// </summary>
public sealed class StubEventHandlerInvoker : IEventHandlerInvoker
{
    /// <inheritdoc />
    public Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType) => Task.CompletedTask;
}

/// <summary>
/// 测试桩：时钟，返回固定时间以保证断言稳定
/// </summary>
public sealed class StubClock : IClock
{
    private static readonly DateTime Fixed = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public DateTime Now => Fixed;

    /// <inheritdoc />
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => false;

    /// <inheritdoc />
    public DateTime Normalize(DateTime dateTime) => dateTime;

    /// <inheritdoc />
    public DateTime ConvertToUserTime(DateTime utcDateTime) => utcDateTime;

    /// <inheritdoc />
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset) => dateTimeOffset;

    /// <inheritdoc />
    public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
}

/// <summary>
/// 测试桩：标识生成器，按顺序产出可预期的标识
/// </summary>
public sealed class StubGuidGenerator : IDistributedIdGenerator<Guid>
{
    private int _counter;

    /// <inheritdoc />
    public Guid NextId()
    {
        var next = Interlocked.Increment(ref _counter);
        var bytes = new byte[16];
        BitConverter.GetBytes(next).CopyTo(bytes, 0);

        return new Guid(bytes);
    }

    /// <inheritdoc />
    public string NextIdString() => NextId().ToString();

    /// <inheritdoc />
    public Guid[] NextIds(int count) => [.. Enumerable.Range(0, count).Select(_ => NextId())];

    /// <inheritdoc />
    public string[] NextIdStrings(int count) => [.. NextIds(count).Select(id => id.ToString())];

    /// <inheritdoc />
    public Task<Guid> NextIdAsync() => Task.FromResult(NextId());

    /// <inheritdoc />
    public Task<string> NextIdStringAsync() => Task.FromResult(NextIdString());

    /// <inheritdoc />
    public Task<Guid[]> NextIdsAsync(int count) => Task.FromResult(NextIds(count));

    /// <inheritdoc />
    public Task<string[]> NextIdStringsAsync(int count) => Task.FromResult(NextIdStrings(count));

    /// <inheritdoc />
    public DateTime ExtractTime(Guid id) => throw new NotSupportedException();

    /// <inheritdoc />
    public int ExtractWorkerId(Guid id) => throw new NotSupportedException();

    /// <inheritdoc />
    public int ExtractSequence(Guid id) => throw new NotSupportedException();

    /// <inheritdoc />
    public int ExtractDataCenterId(Guid id) => throw new NotSupportedException();

    /// <inheritdoc />
    public string GetGeneratorType() => nameof(StubGuidGenerator);

    /// <inheritdoc />
    public Dictionary<string, object> GetStats() => [];
}

/// <summary>
/// 测试桩：空释放器
/// </summary>
public sealed class StubDisposable : IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
    }
}
