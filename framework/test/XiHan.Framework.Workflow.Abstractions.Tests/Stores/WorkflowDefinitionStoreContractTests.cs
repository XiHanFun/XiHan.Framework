// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程定义存储契约测试
/// </summary>
/// <remarks>
/// 接口的 XML 注释把查询语义写成了硬契约（最新已发布 = Code 匹配且已发布、按版本降序取第一条；
/// 最大版本号在编码不存在时返回 0；列表按编码升序、版本降序）。这些语义决定了"启动最新版本"取到哪个版本，
/// 换持久化实现时最容易走样，因此这里写一份最小内存参考实现把契约变成可执行断言。
/// 同时锁死可选参数默认值——默认值属于公共契约，改动即破坏所有省略参数的调用方。
/// </remarks>
public class WorkflowDefinitionStoreContractTests
{
    /// <summary>
    /// 列表查询省略参数时按不过滤处理
    /// </summary>
    [Fact]
    public async Task GetListAsync_WithoutArguments_PassesNullFiltersAndNoneToken()
    {
        var store = new ReferenceDefinitionStore();
        IWorkflowDefinitionStore contract = store;

        await contract.GetListAsync();

        Assert.Null(store.LastListCode);
        Assert.Null(store.LastListStatus);
        Assert.Equal(CancellationToken.None, store.LastToken);
    }

    /// <summary>
    /// 列表查询可只指定状态过滤
    /// </summary>
    [Fact]
    public async Task GetListAsync_WithStatusOnly_KeepsCodeFilterNull()
    {
        var store = new ReferenceDefinitionStore();

        await store.GetListAsync(status: WorkflowDefinitionStatus.Published, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(store.LastListCode);
        Assert.Equal(WorkflowDefinitionStatus.Published, store.LastListStatus);
    }

    /// <summary>
    /// 列表结果按编码升序、版本降序排列
    /// </summary>
    [Fact]
    public async Task GetListAsync_Ordering_IsCodeAscendingThenVersionDescending()
    {
        var store = CreateSeededStore();

        var list = await store.GetListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { "expense/1", "leave/3", "leave/2", "leave/1" },
            list.Select(item => $"{item.Code}/{item.Version}").ToArray());
    }

    /// <summary>
    /// 按编码与版本精确查找
    /// </summary>
    [Fact]
    public async Task FindByVersionAsync_ReturnsExactVersionOrNull()
    {
        var store = CreateSeededStore();

        var found = await store.FindByVersionAsync("leave", 2, TestContext.Current.CancellationToken);
        var missing = await store.FindByVersionAsync("leave", 9, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(2, found.Version);
        Assert.Null(missing);
    }

    /// <summary>
    /// 最新已发布跳过版本更高的草稿
    /// </summary>
    /// <remarks>
    /// 这是最容易写错的一条：草稿版本号总是最大的，若实现只按版本降序取第一条而漏了状态过滤，
    /// 启动实例就会用上未发布的草稿。
    /// </remarks>
    [Fact]
    public async Task FindLatestPublishedAsync_SkipsHigherVersionedDraft()
    {
        var store = CreateSeededStore();

        var latest = await store.FindLatestPublishedAsync("leave", TestContext.Current.CancellationToken);

        Assert.NotNull(latest);
        Assert.Equal(2, latest.Version);
        Assert.Equal(WorkflowDefinitionStatus.Published, latest.Status);
    }

    /// <summary>
    /// 编码不存在时最新已发布返回空
    /// </summary>
    [Fact]
    public async Task FindLatestPublishedAsync_WithUnknownCode_ReturnsNull()
    {
        var store = CreateSeededStore();

        Assert.Null(await store.FindLatestPublishedAsync("unknown", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 最大版本号跨状态统计，编码不存在返回 0
    /// </summary>
    /// <remarks>
    /// 返回 0 而不是抛异常是新建定义能从版本 1 起跑的前提（0 + 1 = 1）。
    /// </remarks>
    [Fact]
    public async Task GetMaxVersionAsync_CountsAllStatusesAndReturnsZeroWhenAbsent()
    {
        var store = CreateSeededStore();

        Assert.Equal(3, await store.GetMaxVersionAsync("leave", TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.GetMaxVersionAsync("unknown", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 插入后可按标识查回，删除后查不到
    /// </summary>
    [Fact]
    public async Task InsertUpdateDelete_RoundTripsByIdentity()
    {
        var store = new ReferenceDefinitionStore();
        var token = TestContext.Current.CancellationToken;

        await store.InsertAsync(new WorkflowDefinition { Id = "def-1", Code = "leave", Version = 1 }, token);
        Assert.NotNull(await store.FindAsync("def-1", token));

        await store.UpdateAsync(new WorkflowDefinition { Id = "def-1", Code = "leave", Version = 1, Name = "改名后" }, token);
        var updated = await store.FindAsync("def-1", token);
        Assert.NotNull(updated);
        Assert.Equal("改名后", updated.Name);

        await store.DeleteAsync("def-1", token);
        Assert.Null(await store.FindAsync("def-1", token));
    }

    /// <summary>
    /// 构造带三个版本请假流程与一个报销流程的存储
    /// </summary>
    /// <returns>参考实现存储</returns>
    private static ReferenceDefinitionStore CreateSeededStore()
    {
        var store = new ReferenceDefinitionStore();
        store.Seed(new WorkflowDefinition { Id = "d1", Code = "leave", Version = 1, Status = WorkflowDefinitionStatus.Disabled });
        store.Seed(new WorkflowDefinition { Id = "d2", Code = "leave", Version = 2, Status = WorkflowDefinitionStatus.Published });
        store.Seed(new WorkflowDefinition { Id = "d3", Code = "leave", Version = 3, Status = WorkflowDefinitionStatus.Draft });
        store.Seed(new WorkflowDefinition { Id = "d4", Code = "expense", Version = 1, Status = WorkflowDefinitionStatus.Published });
        return store;
    }

    /// <summary>
    /// 流程定义存储的最小内存参考实现（严格按接口注释的语义契约实现）
    /// </summary>
    private sealed class ReferenceDefinitionStore : IWorkflowDefinitionStore
    {
        private readonly List<WorkflowDefinition> _definitions = [];

        /// <summary>
        /// 最近一次列表查询的编码过滤
        /// </summary>
        public string? LastListCode { get; private set; }

        /// <summary>
        /// 最近一次列表查询的状态过滤
        /// </summary>
        public WorkflowDefinitionStatus? LastListStatus { get; private set; }

        /// <summary>
        /// 最近一次调用收到的取消令牌
        /// </summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>
        /// 直接灌入定义（绕过插入路径，便于构造场景）
        /// </summary>
        /// <param name="definition">定义</param>
        public void Seed(WorkflowDefinition definition)
        {
            _definitions.Add(definition);
        }

        /// <summary>
        /// 按标识查找定义
        /// </summary>
        /// <param name="id">定义标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>定义</returns>
        public Task<WorkflowDefinition?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult<WorkflowDefinition?>(_definitions.FirstOrDefault(item => item.Id == id));
        }

        /// <summary>
        /// 按编码和版本查找定义
        /// </summary>
        /// <param name="code">流程编码</param>
        /// <param name="version">版本号</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>定义</returns>
        public Task<WorkflowDefinition?> FindByVersionAsync(string code, int version, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult<WorkflowDefinition?>(
                _definitions.FirstOrDefault(item => item.Code == code && item.Version == version));
        }

        /// <summary>
        /// 查找编码下最新的已发布定义
        /// </summary>
        /// <param name="code">流程编码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>定义</returns>
        public Task<WorkflowDefinition?> FindLatestPublishedAsync(string code, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult<WorkflowDefinition?>(_definitions
                .Where(item => item.Code == code && item.Status == WorkflowDefinitionStatus.Published)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault());
        }

        /// <summary>
        /// 获取编码下的最大版本号
        /// </summary>
        /// <param name="code">流程编码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>最大版本号</returns>
        public Task<int> GetMaxVersionAsync(string code, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            var versions = _definitions.Where(item => item.Code == code).Select(item => item.Version).ToList();
            return Task.FromResult(versions.Count == 0 ? 0 : versions.Max());
        }

        /// <summary>
        /// 查询定义列表
        /// </summary>
        /// <param name="code">流程编码</param>
        /// <param name="status">状态</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>定义列表</returns>
        public Task<List<WorkflowDefinition>> GetListAsync(
            string? code = null,
            WorkflowDefinitionStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            LastListCode = code;
            LastListStatus = status;
            LastToken = cancellationToken;

            var query = _definitions.AsEnumerable();
            if (code is not null)
            {
                query = query.Where(item => item.Code == code);
            }

            if (status is not null)
            {
                query = query.Where(item => item.Status == status);
            }

            return Task.FromResult(query
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenByDescending(item => item.Version)
                .ToList());
        }

        /// <summary>
        /// 插入定义
        /// </summary>
        /// <param name="definition">定义</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task InsertAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _definitions.Add(definition);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新定义
        /// </summary>
        /// <param name="definition">定义</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _definitions.RemoveAll(item => item.Id == definition.Id);
            _definitions.Add(definition);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 删除定义
        /// </summary>
        /// <param name="id">定义标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            _definitions.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }
}
