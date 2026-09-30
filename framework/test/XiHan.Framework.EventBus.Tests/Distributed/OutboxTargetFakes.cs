// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Linq.Expressions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 按当前租户分区存放事件的发件箱替身，宿主布局的分区键为 0
/// </summary>
public sealed class TenantScopedFakeOutbox : ITenantScopedEventOutbox
{
    private readonly ICurrentTenant _currentTenant;
    private readonly Dictionary<long, List<OutgoingEventInfo>> _pending = [];
    private readonly Dictionary<long, List<OutgoingEventInfo>> _claimed = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户</param>
    public TenantScopedFakeOutbox(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 领取时抛出异常的分区
    /// </summary>
    public HashSet<long> FaultyTenants { get; } = [];

    /// <summary>
    /// 按调用顺序记录的领取请求
    /// </summary>
    public List<(long TenantId, int MaxCount)> ClaimRequests { get; } = [];

    /// <summary>
    /// 每次领取时回调
    /// </summary>
    public Action? OnClaim { get; set; }

    /// <summary>
    /// 全部分区中尚未删除的事件数
    /// </summary>
    public int TotalRemaining
    {
        get { return _pending.Values.Sum(items => items.Count) + _claimed.Values.Sum(items => items.Count); }
    }

    /// <summary>
    /// 向指定分区放入事件
    /// </summary>
    /// <param name="tenantId">分区键，宿主为 0</param>
    /// <param name="count">事件数</param>
    public void Seed(long tenantId, int count)
    {
        var items = GetList(_pending, tenantId);
        for (var index = 0; index < count; index++)
        {
            items.Add(new OutgoingEventInfo(Guid.NewGuid(), "Test.Event", [1], DateTime.UtcNow));
        }
    }

    /// <summary>
    /// 指定分区尚未删除的事件数
    /// </summary>
    /// <param name="tenantId">分区键，宿主为 0</param>
    /// <returns>事件数</returns>
    public int RemainingOf(long tenantId)
    {
        return GetList(_pending, tenantId).Count + GetList(_claimed, tenantId).Count;
    }

    /// <summary>
    /// 加入出站事件
    /// </summary>
    /// <param name="outgoingEvent">出站事件</param>
    /// <returns>任务</returns>
    public Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        GetList(_pending, CurrentKey()).Add(outgoingEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 领取当前分区的待发送事件
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>领取到的事件</returns>
    public Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var key = CurrentKey();
        ClaimRequests.Add((key, maxCount));
        OnClaim?.Invoke();

        if (FaultyTenants.Contains(key))
        {
            throw new InvalidOperationException("模拟目标库不可达。");
        }

        var pending = GetList(_pending, key);
        var taken = pending.Take(maxCount).ToList();
        pending.RemoveRange(0, taken.Count);
        GetList(_claimed, key).AddRange(taken);

        return Task.FromResult(taken);
    }

    /// <summary>
    /// 删除出站事件
    /// </summary>
    /// <param name="id">事件标识</param>
    /// <returns>任务</returns>
    public Task DeleteAsync(Guid id)
    {
        return DeleteManyAsync([id]);
    }

    /// <summary>
    /// 只在当前分区删除已领取的事件
    /// </summary>
    /// <param name="ids">事件标识</param>
    /// <returns>任务</returns>
    public Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        GetList(_claimed, CurrentKey()).RemoveAll(item => idSet.Contains(item.Id));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当前分区尚未删除的事件数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件数</returns>
    public Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((long)RemainingOf(CurrentKey()));
    }

    private long CurrentKey()
    {
        return _currentTenant.Id ?? 0;
    }

    private static List<OutgoingEventInfo> GetList(Dictionary<long, List<OutgoingEventInfo>> source, long key)
    {
        if (!source.TryGetValue(key, out var items))
        {
            items = [];
            source[key] = items;
        }

        return items;
    }
}

/// <summary>
/// 测试用投递目标目录，游标为下一页起始位置的十进制字符串
/// </summary>
public sealed class FakeTargetProvider : IOutboxDeliveryTargetProvider
{
    /// <summary>
    /// 目录中的目标
    /// </summary>
    public List<OutboxDeliveryTarget> Targets { get; } = [];

    /// <summary>
    /// 读取时抛出异常的游标，首页以空字符串表示
    /// </summary>
    public HashSet<string> FailingCursors { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 读取时返回空集合但仍给出下一页游标的游标，首页以空字符串表示
    /// </summary>
    public HashSet<string> EmptyCursors { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 按调用顺序记录的读取游标
    /// </summary>
    public List<string?> RequestedCursors { get; } = [];

    /// <summary>
    /// 分页读取目录
    /// </summary>
    /// <param name="cursor">游标</param>
    /// <param name="pageSize">单页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>一页目标</returns>
    public Task<OutboxDeliveryTargetPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        RequestedCursors.Add(cursor);

        if (FailingCursors.Contains(cursor ?? string.Empty))
        {
            throw new InvalidOperationException("模拟投递目标目录不可用。");
        }

        var start = cursor is null ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);
        var items = EmptyCursors.Contains(cursor ?? string.Empty)
            ? []
            : Targets.Skip(start).Take(pageSize).ToList();
        var next = EmptyCursors.Contains(cursor ?? string.Empty) ? start + pageSize : start + items.Count;

        return Task.FromResult(new OutboxDeliveryTargetPage(
            items,
            next < Targets.Count ? next.ToString(CultureInfo.InvariantCulture) : null));
    }

    /// <summary>
    /// 按租户标识查找目标
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>目标，不在目录中时为 null</returns>
    public Task<OutboxDeliveryTarget?> FindAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Targets.FirstOrDefault(target => target.TenantId == tenantId));
    }
}

/// <summary>
/// 可手动推进的时间提供器
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 当前时间
    /// </summary>
    /// <returns>当前时间</returns>
    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    /// <summary>
    /// 推进时间
    /// </summary>
    /// <param name="duration">推进时长</param>
    public void Advance(TimeSpan duration)
    {
        _now += duration;
    }
}
