// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程书签存储契约测试
/// </summary>
/// <remarks>
/// 两条查询语义是整个挂起恢复机制的命门，接口注释已写成硬契约，这里用最小内存参考实现固化：
/// 到期查询必须是 <c>DueTime 非空且 &lt;= now</c>、按到期时间升序、限量返回（漏掉"非空"判断会把人工任务书签也当成定时器扫走）；
/// 信号查询在传入相关性标识时必须放行"书签自身未声明相关性"的广播订阅者（漏掉这一支会让通用监听器永远收不到定向信号）。
/// </remarks>
public class WorkflowBookmarkStoreContractTests
{
    /// <summary>
    /// 到期查询排除无到期时间的书签
    /// </summary>
    [Fact]
    public async Task GetDueAsync_ExcludesBookmarksWithoutDueTime()
    {
        var store = CreateSeededStore();
        var now = new DateTime(2024, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        var due = await store.GetDueAsync(now, 10, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(due, item => item.DueTime is null);
        Assert.DoesNotContain(due, item => item.Kind == WorkflowBookmarkKinds.UserTask);
    }

    /// <summary>
    /// 到期查询按到期时间升序且排除未到期书签
    /// </summary>
    [Fact]
    public async Task GetDueAsync_OrdersByDueTimeAscendingAndSkipsFutureOnes()
    {
        var store = CreateSeededStore();
        var now = new DateTime(2024, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        var due = await store.GetDueAsync(now, 10, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "bm-timer-past", "bm-retry-now" }, due.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 到期时间恰好等于当前时间的书签算作已到期
    /// </summary>
    [Fact]
    public async Task GetDueAsync_TreatsExactDueTimeAsDue()
    {
        var store = CreateSeededStore();
        var now = new DateTime(2024, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        var due = await store.GetDueAsync(now, 10, TestContext.Current.CancellationToken);

        Assert.Contains(due, item => item.Id == "bm-retry-now");
    }

    /// <summary>
    /// 到期查询受最大条数限制
    /// </summary>
    [Fact]
    public async Task GetDueAsync_HonorsMaxResultCount()
    {
        var store = CreateSeededStore();
        var now = new DateTime(2024, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        var due = await store.GetDueAsync(now, 1, TestContext.Current.CancellationToken);

        Assert.Single(due);
        Assert.Equal("bm-timer-past", due[0].Id);
    }

    /// <summary>
    /// 按种类和索引键查询返回创建时间升序的结果
    /// </summary>
    [Fact]
    public async Task GetByKindAndKeyAsync_OrdersByCreationTimeAscending()
    {
        var store = CreateSeededStore();

        var tasks = await store.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, "u-1", TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "bm-task-1", "bm-task-2" }, tasks.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 广播信号命中全部同名信号书签，不看相关性标识
    /// </summary>
    [Fact]
    public async Task GetBySignalAsync_WithoutCorrelationId_MatchesAllSameNamedSignals()
    {
        var store = CreateSeededStore();

        var matched = await store.GetBySignalAsync("paid", null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "bm-signal-any", "bm-signal-biz1", "bm-signal-biz2" }, matched.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 定向信号同时命中相关性相等与未声明相关性的书签
    /// </summary>
    [Fact]
    public async Task GetBySignalAsync_WithCorrelationId_AlsoMatchesUnboundBookmarks()
    {
        var store = CreateSeededStore();

        var matched = await store.GetBySignalAsync("paid", "biz-1", TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "bm-signal-any", "bm-signal-biz1" }, matched.Select(item => item.Id).ToArray());
        Assert.DoesNotContain(matched, item => item.Id == "bm-signal-biz2");
    }

    /// <summary>
    /// 信号查询按信号名精确匹配，不命中其他信号
    /// </summary>
    [Fact]
    public async Task GetBySignalAsync_WithOtherSignalName_ReturnsEmpty()
    {
        var store = CreateSeededStore();

        Assert.Empty(await store.GetBySignalAsync("shipped", null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 按实例与按节点实例分别聚合书签
    /// </summary>
    [Fact]
    public async Task GetByInstanceAndByNodeInstance_ScopeResultsCorrectly()
    {
        var store = CreateSeededStore();
        var token = TestContext.Current.CancellationToken;

        var byInstance = await store.GetByInstanceAsync("ins-1", token);
        var byNodeInstance = await store.GetByNodeInstanceAsync("ni-1", token);

        Assert.Equal(2, byInstance.Count);
        Assert.Equal(2, byNodeInstance.Count);
        Assert.DoesNotContain(byNodeInstance, item => item.NodeInstanceId != "ni-1");
    }

    /// <summary>
    /// 按实例删除清空该实例全部书签且不影响其他实例
    /// </summary>
    [Fact]
    public async Task DeleteByInstanceAsync_RemovesOnlyThatInstanceBookmarks()
    {
        var store = CreateSeededStore();
        var token = TestContext.Current.CancellationToken;

        await store.DeleteByInstanceAsync("ins-1", token);

        Assert.Empty(await store.GetByInstanceAsync("ins-1", token));
        Assert.NotEmpty(await store.GetByInstanceAsync("ins-2", token));
    }

    /// <summary>
    /// 单条删除后查不到，更新后可查回最新内容
    /// </summary>
    [Fact]
    public async Task InsertUpdateDelete_RoundTripsByIdentity()
    {
        var store = new ReferenceBookmarkStore();
        var token = TestContext.Current.CancellationToken;

        await store.InsertAsync(new WorkflowBookmark { Id = "bm-1", Kind = WorkflowBookmarkKinds.Signal, Key = "paid" }, token);
        Assert.NotNull(await store.FindAsync("bm-1", token));

        await store.UpdateAsync(new WorkflowBookmark { Id = "bm-1", Kind = WorkflowBookmarkKinds.Signal, Key = "shipped" }, token);
        var updated = await store.FindAsync("bm-1", token);
        Assert.NotNull(updated);
        Assert.Equal("shipped", updated.Key);

        await store.DeleteAsync("bm-1", token);
        Assert.Null(await store.FindAsync("bm-1", token));
    }

    /// <summary>
    /// 构造覆盖三类书签场景的存储
    /// </summary>
    /// <returns>参考实现存储</returns>
    private static ReferenceBookmarkStore CreateSeededStore()
    {
        var store = new ReferenceBookmarkStore();
        var baseTime = new DateTime(2024, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2024, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        store.Seed(new WorkflowBookmark
        {
            Id = "bm-task-1",
            InstanceId = "ins-1",
            NodeInstanceId = "ni-1",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u-1",
            CreationTime = baseTime
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-task-2",
            InstanceId = "ins-1",
            NodeInstanceId = "ni-1",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u-1",
            CreationTime = baseTime.AddMinutes(5)
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-timer-past",
            InstanceId = "ins-2",
            NodeInstanceId = "ni-2",
            Kind = WorkflowBookmarkKinds.Timer,
            DueTime = now.AddHours(-1),
            CreationTime = baseTime
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-retry-now",
            InstanceId = "ins-2",
            NodeInstanceId = "ni-2",
            Kind = WorkflowBookmarkKinds.Retry,
            DueTime = now,
            CreationTime = baseTime
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-timeout-future",
            InstanceId = "ins-2",
            NodeInstanceId = "ni-3",
            Kind = WorkflowBookmarkKinds.NodeTimeout,
            DueTime = now.AddHours(1),
            CreationTime = baseTime
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-signal-any",
            InstanceId = "ins-3",
            NodeInstanceId = "ni-4",
            Kind = WorkflowBookmarkKinds.Signal,
            Key = "paid",
            CorrelationId = null,
            CreationTime = baseTime
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-signal-biz1",
            InstanceId = "ins-3",
            NodeInstanceId = "ni-4",
            Kind = WorkflowBookmarkKinds.Signal,
            Key = "paid",
            CorrelationId = "biz-1",
            CreationTime = baseTime.AddMinutes(1)
        });
        store.Seed(new WorkflowBookmark
        {
            Id = "bm-signal-biz2",
            InstanceId = "ins-3",
            NodeInstanceId = "ni-4",
            Kind = WorkflowBookmarkKinds.Signal,
            Key = "paid",
            CorrelationId = "biz-2",
            CreationTime = baseTime.AddMinutes(2)
        });

        return store;
    }

    /// <summary>
    /// 流程书签存储的最小内存参考实现（严格按接口注释的语义契约实现）
    /// </summary>
    private sealed class ReferenceBookmarkStore : IWorkflowBookmarkStore
    {
        private readonly List<WorkflowBookmark> _bookmarks = [];

        /// <summary>
        /// 直接灌入书签
        /// </summary>
        /// <param name="bookmark">书签</param>
        public void Seed(WorkflowBookmark bookmark)
        {
            _bookmarks.Add(bookmark);
        }

        /// <summary>
        /// 按标识查找书签
        /// </summary>
        /// <param name="id">书签标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>书签</returns>
        public Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkflowBookmark?>(_bookmarks.FirstOrDefault(item => item.Id == id));
        }

        /// <summary>
        /// 获取实例的全部书签
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>书签列表</returns>
        public Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bookmarks.Where(item => item.InstanceId == instanceId).ToList());
        }

        /// <summary>
        /// 获取节点实例的全部书签
        /// </summary>
        /// <param name="nodeInstanceId">节点实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>书签列表</returns>
        public Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bookmarks.Where(item => item.NodeInstanceId == nodeInstanceId).ToList());
        }

        /// <summary>
        /// 获取到期的定时类书签
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <param name="maxResultCount">最大返回条数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>到期书签列表</returns>
        public Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bookmarks
                .Where(item => item.DueTime is not null && item.DueTime <= now)
                .OrderBy(item => item.DueTime)
                .Take(maxResultCount)
                .ToList());
        }

        /// <summary>
        /// 按种类和索引键查询书签
        /// </summary>
        /// <param name="kind">书签种类</param>
        /// <param name="key">索引键</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>书签列表</returns>
        public Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bookmarks
                .Where(item => item.Kind == kind && item.Key == key)
                .OrderBy(item => item.CreationTime)
                .ToList());
        }

        /// <summary>
        /// 查询匹配信号的书签
        /// </summary>
        /// <param name="signalName">信号名称</param>
        /// <param name="correlationId">业务相关性标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>书签列表</returns>
        public Task<List<WorkflowBookmark>> GetBySignalAsync(
            string signalName,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            var query = _bookmarks.Where(item => item.Kind == WorkflowBookmarkKinds.Signal && item.Key == signalName);

            if (correlationId is not null)
            {
                query = query.Where(item => item.CorrelationId is null || item.CorrelationId == correlationId);
            }

            return Task.FromResult(query.OrderBy(item => item.CreationTime).ToList());
        }

        /// <summary>
        /// 插入书签
        /// </summary>
        /// <param name="bookmark">书签</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
        {
            _bookmarks.Add(bookmark);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新书签
        /// </summary>
        /// <param name="bookmark">书签</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
        {
            _bookmarks.RemoveAll(item => item.Id == bookmark.Id);
            _bookmarks.Add(bookmark);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 删除书签
        /// </summary>
        /// <param name="id">书签标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _bookmarks.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 删除实例的全部书签
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            _bookmarks.RemoveAll(item => item.InstanceId == instanceId);
            return Task.CompletedTask;
        }
    }
}
