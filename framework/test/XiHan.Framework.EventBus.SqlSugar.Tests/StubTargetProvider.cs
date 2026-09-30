// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 测试用投递目标目录，游标为下一页起始位置的十进制字符串
/// </summary>
internal sealed class StubTargetProvider : IOutboxDeliveryTargetProvider
{
    /// <summary>
    /// 目录中的目标，按顺序分页
    /// </summary>
    public List<OutboxDeliveryTarget> Targets { get; } = [];

    /// <summary>
    /// 读取时抛出异常的游标，首页以空字符串表示
    /// </summary>
    public HashSet<string> FailingCursors { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 按调用顺序记录的读取游标
    /// </summary>
    public List<string?> RequestedCursors { get; } = [];

    /// <summary>
    /// 读取租户上下文的探针，在 FindAsync 内调用
    /// </summary>
    public Func<long?>? TenantProbe { get; set; }

    /// <summary>
    /// 按调用顺序记录的 FindAsync 内租户上下文
    /// </summary>
    public List<long?> FindTenantContexts { get; } = [];

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
        var items = Targets.Skip(start).Take(pageSize).ToList();
        var next = start + items.Count;

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
        if (TenantProbe is not null)
        {
            FindTenantContexts.Add(TenantProbe());
        }

        return Task.FromResult(Targets.FirstOrDefault(target => target.TenantId == tenantId));
    }

    /// <summary>
    /// 把目录中的目标改为启用或停用
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="isEnabled">是否启用</param>
    public void SetEnabled(long tenantId, bool isEnabled)
    {
        var index = Targets.FindIndex(target => target.TenantId == tenantId);
        Targets[index] = new OutboxDeliveryTarget(tenantId, Targets[index].TenantName, isEnabled);
    }
}
