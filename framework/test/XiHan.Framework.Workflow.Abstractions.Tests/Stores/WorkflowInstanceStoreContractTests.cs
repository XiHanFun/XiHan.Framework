// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程实例存储契约测试
/// </summary>
/// <remarks>
/// 三条语义契约必须锁死：列表默认最多 100 条且按创建时间降序、子实例按创建时间升序、
/// 节点实例按开始时间升序且同刻保持写入先后——最后一条是补偿逆序执行的唯一依据，
/// 若实现用了不稳定排序，同一毫秒内完成的两个节点补偿顺序就会随机翻转。
/// 这里用最小内存参考实现把契约变成可执行断言。
/// </remarks>
public class WorkflowInstanceStoreContractTests
{
    /// <summary>
    /// 列表查询省略参数时不过滤且默认最多 100 条
    /// </summary>
    [Fact]
    public async Task GetListAsync_WithoutArguments_UsesNullFiltersAndHundredLimit()
    {
        var store = new ReferenceInstanceStore();
        IWorkflowInstanceStore contract = store;

        await contract.GetListAsync();

        Assert.Null(store.LastStatus);
        Assert.Null(store.LastDefinitionCode);
        Assert.Null(store.LastCorrelationId);
        Assert.Equal(100, store.LastMaxResultCount);
        Assert.Equal(CancellationToken.None, store.LastToken);
    }

    /// <summary>
    /// 列表结果按创建时间降序并受最大条数限制
    /// </summary>
    [Fact]
    public async Task GetListAsync_OrdersByCreationTimeDescendingAndHonorsLimit()
    {
        var store = CreateSeededStore();

        var all = await store.GetListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var limited = await store.GetListAsync(maxResultCount: 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "ins-3", "ins-2", "ins-1" }, all.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { "ins-3", "ins-2" }, limited.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 列表查询按状态、定义编码与相关性标识联合过滤
    /// </summary>
    [Fact]
    public async Task GetListAsync_FiltersByStatusCodeAndCorrelationId()
    {
        var store = CreateSeededStore();
        var token = TestContext.Current.CancellationToken;

        var running = await store.GetListAsync(status: WorkflowInstanceStatus.Running, cancellationToken: token);
        var byCode = await store.GetListAsync(definitionCode: "expense", cancellationToken: token);
        var byCorrelation = await store.GetListAsync(correlationId: "biz-1", cancellationToken: token);

        Assert.Equal(new[] { "ins-3", "ins-1" }, running.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { "ins-2" }, byCode.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { "ins-1" }, byCorrelation.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 子实例按创建时间升序返回，只包含直接子级
    /// </summary>
    [Fact]
    public async Task GetChildrenAsync_ReturnsDirectChildrenInCreationOrder()
    {
        var store = CreateSeededStore();

        var children = await store.GetChildrenAsync("ins-1", TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "ins-2", "ins-3" }, children.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 节点实例按开始时间升序返回，同刻保持写入先后
    /// </summary>
    [Fact]
    public async Task GetNodeInstancesAsync_OrdersByStartTimeAndKeepsInsertionOrderOnTies()
    {
        var store = new ReferenceInstanceStore();
        var token = TestContext.Current.CancellationToken;
        var sameMoment = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

        await store.InsertNodeInstanceAsync(
            new WorkflowNodeInstance { Id = "n2", InstanceId = "ins-1", StartTime = sameMoment }, token);
        await store.InsertNodeInstanceAsync(
            new WorkflowNodeInstance { Id = "n3", InstanceId = "ins-1", StartTime = sameMoment }, token);
        await store.InsertNodeInstanceAsync(
            new WorkflowNodeInstance { Id = "n1", InstanceId = "ins-1", StartTime = sameMoment.AddSeconds(-1) }, token);

        var nodeInstances = await store.GetNodeInstancesAsync("ins-1", token);

        Assert.Equal(new[] { "n1", "n2", "n3" }, nodeInstances.Select(item => item.Id).ToArray());
    }

    /// <summary>
    /// 删除实例级联删除其节点实例
    /// </summary>
    [Fact]
    public async Task DeleteAsync_CascadesToNodeInstances()
    {
        var store = new ReferenceInstanceStore();
        var token = TestContext.Current.CancellationToken;

        await store.InsertAsync(new WorkflowInstance { Id = "ins-1" }, token);
        await store.InsertNodeInstanceAsync(new WorkflowNodeInstance { Id = "n1", InstanceId = "ins-1" }, token);
        await store.InsertNodeInstanceAsync(new WorkflowNodeInstance { Id = "n2", InstanceId = "ins-2" }, token);

        await store.DeleteAsync("ins-1", token);

        Assert.Null(await store.FindAsync("ins-1", token));
        Assert.Empty(await store.GetNodeInstancesAsync("ins-1", token));
        Assert.NotNull(await store.FindNodeInstanceAsync("n2", token));
    }

    /// <summary>
    /// 更新实例后按标识查回最新内容
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ReplacesStoredInstance()
    {
        var store = new ReferenceInstanceStore();
        var token = TestContext.Current.CancellationToken;

        await store.InsertAsync(new WorkflowInstance { Id = "ins-1" }, token);
        await store.UpdateAsync(new WorkflowInstance { Id = "ins-1", Status = WorkflowInstanceStatus.Completed }, token);

        var restored = await store.FindAsync("ins-1", token);

        Assert.NotNull(restored);
        Assert.True(restored.IsFinalStatus());
    }

    /// <summary>
    /// 更新节点实例后按标识查回最新内容
    /// </summary>
    [Fact]
    public async Task UpdateNodeInstanceAsync_ReplacesStoredNodeInstance()
    {
        var store = new ReferenceInstanceStore();
        var token = TestContext.Current.CancellationToken;

        await store.InsertNodeInstanceAsync(new WorkflowNodeInstance { Id = "n1", InstanceId = "ins-1" }, token);
        await store.UpdateNodeInstanceAsync(
            new WorkflowNodeInstance { Id = "n1", InstanceId = "ins-1", Status = WorkflowNodeInstanceStatus.Completed, TryCount = 2 },
            token);

        var restored = await store.FindNodeInstanceAsync("n1", token);

        Assert.NotNull(restored);
        Assert.Equal(WorkflowNodeInstanceStatus.Completed, restored.Status);
        Assert.Equal(2, restored.TryCount);
    }

    /// <summary>
    /// 构造一个父实例带两个子实例的存储
    /// </summary>
    /// <returns>参考实现存储</returns>
    private static ReferenceInstanceStore CreateSeededStore()
    {
        var store = new ReferenceInstanceStore();
        var baseTime = new DateTime(2024, 5, 6, 0, 0, 0, DateTimeKind.Utc);

        store.Seed(new WorkflowInstance
        {
            Id = "ins-1",
            DefinitionCode = "leave",
            CorrelationId = "biz-1",
            Status = WorkflowInstanceStatus.Running,
            CreationTime = baseTime
        });
        store.Seed(new WorkflowInstance
        {
            Id = "ins-2",
            DefinitionCode = "expense",
            Status = WorkflowInstanceStatus.Completed,
            ParentInstanceId = "ins-1",
            CreationTime = baseTime.AddMinutes(1)
        });
        store.Seed(new WorkflowInstance
        {
            Id = "ins-3",
            DefinitionCode = "leave",
            Status = WorkflowInstanceStatus.Running,
            ParentInstanceId = "ins-1",
            CreationTime = baseTime.AddMinutes(2)
        });

        return store;
    }

    /// <summary>
    /// 流程实例存储的最小内存参考实现（严格按接口注释的语义契约实现）
    /// </summary>
    private sealed class ReferenceInstanceStore : IWorkflowInstanceStore
    {
        private readonly List<WorkflowInstance> _instances = [];
        private readonly List<WorkflowNodeInstance> _nodeInstances = [];

        /// <summary>
        /// 最近一次列表查询的状态过滤
        /// </summary>
        public WorkflowInstanceStatus? LastStatus { get; private set; }

        /// <summary>
        /// 最近一次列表查询的定义编码过滤
        /// </summary>
        public string? LastDefinitionCode { get; private set; }

        /// <summary>
        /// 最近一次列表查询的相关性标识过滤
        /// </summary>
        public string? LastCorrelationId { get; private set; }

        /// <summary>
        /// 最近一次列表查询的最大条数
        /// </summary>
        public int LastMaxResultCount { get; private set; }

        /// <summary>
        /// 最近一次调用收到的取消令牌
        /// </summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>
        /// 直接灌入实例
        /// </summary>
        /// <param name="instance">实例</param>
        public void Seed(WorkflowInstance instance)
        {
            _instances.Add(instance);
        }

        /// <summary>
        /// 按标识查找实例
        /// </summary>
        /// <param name="id">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult<WorkflowInstance?>(_instances.FirstOrDefault(item => item.Id == id));
        }

        /// <summary>
        /// 查询实例列表
        /// </summary>
        /// <param name="status">状态</param>
        /// <param name="definitionCode">定义编码</param>
        /// <param name="correlationId">业务相关性标识</param>
        /// <param name="maxResultCount">最大返回条数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例列表</returns>
        public Task<List<WorkflowInstance>> GetListAsync(
            WorkflowInstanceStatus? status = null,
            string? definitionCode = null,
            string? correlationId = null,
            int maxResultCount = 100,
            CancellationToken cancellationToken = default)
        {
            LastStatus = status;
            LastDefinitionCode = definitionCode;
            LastCorrelationId = correlationId;
            LastMaxResultCount = maxResultCount;
            LastToken = cancellationToken;

            var query = _instances.AsEnumerable();
            if (status is not null)
            {
                query = query.Where(item => item.Status == status);
            }

            if (definitionCode is not null)
            {
                query = query.Where(item => item.DefinitionCode == definitionCode);
            }

            if (correlationId is not null)
            {
                query = query.Where(item => item.CorrelationId == correlationId);
            }

            return Task.FromResult(query
                .OrderByDescending(item => item.CreationTime)
                .Take(maxResultCount)
                .ToList());
        }

        /// <summary>
        /// 获取实例的直接子实例列表
        /// </summary>
        /// <param name="parentInstanceId">父实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>子实例列表</returns>
        public Task<List<WorkflowInstance>> GetChildrenAsync(string parentInstanceId, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult(_instances
                .Where(item => item.ParentInstanceId == parentInstanceId)
                .OrderBy(item => item.CreationTime)
                .ToList());
        }

        /// <summary>
        /// 插入实例
        /// </summary>
        /// <param name="instance">实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task InsertAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _instances.Add(instance);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新实例
        /// </summary>
        /// <param name="instance">实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _instances.RemoveAll(item => item.Id == instance.Id);
            _instances.Add(instance);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 删除实例（级联删除节点实例）
        /// </summary>
        /// <param name="id">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _instances.RemoveAll(item => item.Id == id);
            _nodeInstances.RemoveAll(item => item.InstanceId == id);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 按标识查找节点实例
        /// </summary>
        /// <param name="id">节点实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>节点实例</returns>
        public Task<WorkflowNodeInstance?> FindNodeInstanceAsync(string id, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult<WorkflowNodeInstance?>(_nodeInstances.FirstOrDefault(item => item.Id == id));
        }

        /// <summary>
        /// 获取实例的节点实例列表
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>节点实例列表</returns>
        public Task<List<WorkflowNodeInstance>> GetNodeInstancesAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;

            // OrderBy 是稳定排序，同刻的节点实例保持写入先后，补偿逆序依赖这一点
            return Task.FromResult(_nodeInstances
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.StartTime)
                .ToList());
        }

        /// <summary>
        /// 插入节点实例
        /// </summary>
        /// <param name="nodeInstance">节点实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task InsertNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _nodeInstances.Add(nodeInstance);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新节点实例
        /// </summary>
        /// <param name="nodeInstance">节点实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task UpdateNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            var index = _nodeInstances.FindIndex(item => item.Id == nodeInstance.Id);
            if (index >= 0)
            {
                _nodeInstances[index] = nodeInstance;
            }
            else
            {
                _nodeInstances.Add(nodeInstance);
            }

            return Task.CompletedTask;
        }
    }
}
