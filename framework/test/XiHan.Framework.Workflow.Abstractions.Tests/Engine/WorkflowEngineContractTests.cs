// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Engine;

/// <summary>
/// 工作流引擎接口契约测试
/// </summary>
/// <remarks>
/// 引擎是外部唯一入口，可选参数的默认值就是"省略写法"的实际语义，属于公共契约：
/// 尤其 <c>throwIfNotResumable</c> 默认 true——默认严格失败，静默跳过必须由调用方显式声明；
/// 若哪天默认值反转成 false，所有省略该参数的调用会从抛异常变成静默无操作，编译期毫无提示。
/// 这里用记录式手写实现把默认值与参数传递逐条钉死。
/// </remarks>
public class WorkflowEngineContractTests
{
    /// <summary>
    /// 启动省略取消令牌时传入 None
    /// </summary>
    [Fact]
    public async Task StartAsync_WithoutToken_PassesNoneToken()
    {
        var engine = new RecordingWorkflowEngine();
        IWorkflowEngine contract = engine;
        var request = new WorkflowStartRequest { DefinitionCode = "leave" };

        var instance = await contract.StartAsync(request);

        Assert.Same(request, engine.LastStartRequest);
        Assert.Equal(CancellationToken.None, engine.LastToken);
        Assert.Equal("ins-1", instance.Id);
    }

    /// <summary>
    /// 恢复书签省略可选参数时默认严格失败且无输入
    /// </summary>
    [Fact]
    public async Task ResumeBookmarkAsync_WithoutOptionalArguments_DefaultsToThrowingWithoutInputs()
    {
        var engine = new RecordingWorkflowEngine();

        await engine.ResumeBookmarkAsync("bm-1");

        Assert.Equal("bm-1", engine.LastBookmarkId);
        Assert.Null(engine.LastInputs);
        Assert.True(engine.LastThrowIfNotResumable);
        Assert.Null(engine.LastExpectedBookmarkKey);
        Assert.Equal(CancellationToken.None, engine.LastToken);
    }

    /// <summary>
    /// 恢复书签可显式声明静默跳过并校验期望索引键
    /// </summary>
    [Fact]
    public async Task ResumeBookmarkAsync_WithExplicitArguments_PassesThemThrough()
    {
        var engine = new RecordingWorkflowEngine();
        var inputs = new Dictionary<string, object?> { [WorkflowConsts.OutcomeVariableName] = WorkflowUserTaskOutcomes.Approved };

        await engine.ResumeBookmarkAsync("bm-1", inputs, throwIfNotResumable: false, expectedBookmarkKey: "u-1",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(inputs, engine.LastInputs);
        Assert.False(engine.LastThrowIfNotResumable);
        Assert.Equal("u-1", engine.LastExpectedBookmarkKey);
    }

    /// <summary>
    /// 发布信号省略可选参数时按广播处理
    /// </summary>
    [Fact]
    public async Task PublishSignalAsync_WithoutOptionalArguments_MeansBroadcastWithoutPayload()
    {
        var engine = new RecordingWorkflowEngine();

        var resumedCount = await engine.PublishSignalAsync("paid");

        Assert.Equal("paid", engine.LastSignalName);
        Assert.Null(engine.LastPayload);
        Assert.Null(engine.LastCorrelationId);
        Assert.Equal(0, resumedCount);
    }

    /// <summary>
    /// 发布信号可携带载荷并定向到指定相关性标识
    /// </summary>
    [Fact]
    public async Task PublishSignalAsync_WithCorrelationId_PassesItThrough()
    {
        var engine = new RecordingWorkflowEngine();
        var payload = new Dictionary<string, object?> { ["amount"] = 100 };

        await engine.PublishSignalAsync("paid", payload, "biz-1", TestContext.Current.CancellationToken);

        Assert.Same(payload, engine.LastPayload);
        Assert.Equal("biz-1", engine.LastCorrelationId);
    }

    /// <summary>
    /// 挂起、取消与终止省略原因时传入空
    /// </summary>
    [Fact]
    public async Task LifecycleOperations_WithoutReason_PassNullReason()
    {
        var engine = new RecordingWorkflowEngine();

        await engine.SuspendAsync("ins-1");
        Assert.Null(engine.LastReason);

        await engine.CancelAsync("ins-1");
        Assert.Null(engine.LastReason);

        await engine.TerminateAsync("ins-1");
        Assert.Null(engine.LastReason);
    }

    /// <summary>
    /// 挂起、取消与终止可携带原因并被记录到实例上
    /// </summary>
    [Fact]
    public async Task LifecycleOperations_WithReason_PassItThrough()
    {
        var engine = new RecordingWorkflowEngine();
        var token = TestContext.Current.CancellationToken;

        await engine.SuspendAsync("ins-1", "等待人工核查", token);
        Assert.Equal("等待人工核查", engine.LastReason);

        await engine.CancelAsync("ins-1", "业务撤单", token);
        Assert.Equal("业务撤单", engine.LastReason);

        await engine.TerminateAsync("ins-1", "管理员强制终止", token);
        Assert.Equal("管理员强制终止", engine.LastReason);
    }

    /// <summary>
    /// 恢复运行与重试仅需实例标识
    /// </summary>
    [Fact]
    public async Task ResumeAndRetry_TakeInstanceIdOnly()
    {
        var engine = new RecordingWorkflowEngine();

        await engine.ResumeAsync("ins-1");
        Assert.Equal("ins-1", engine.LastInstanceId);
        Assert.Equal(CancellationToken.None, engine.LastToken);

        await engine.RetryAsync("ins-2");
        Assert.Equal("ins-2", engine.LastInstanceId);
    }

    /// <summary>
    /// 工作流引擎的记录式手写实现
    /// </summary>
    private sealed class RecordingWorkflowEngine : IWorkflowEngine
    {
        /// <summary>
        /// 最近一次启动请求
        /// </summary>
        public WorkflowStartRequest? LastStartRequest { get; private set; }

        /// <summary>
        /// 最近一次书签标识
        /// </summary>
        public string? LastBookmarkId { get; private set; }

        /// <summary>
        /// 最近一次恢复输入
        /// </summary>
        public Dictionary<string, object?>? LastInputs { get; private set; }

        /// <summary>
        /// 最近一次是否要求不可恢复时抛异常
        /// </summary>
        public bool LastThrowIfNotResumable { get; private set; }

        /// <summary>
        /// 最近一次期望的书签索引键
        /// </summary>
        public string? LastExpectedBookmarkKey { get; private set; }

        /// <summary>
        /// 最近一次信号名称
        /// </summary>
        public string? LastSignalName { get; private set; }

        /// <summary>
        /// 最近一次信号载荷
        /// </summary>
        public Dictionary<string, object?>? LastPayload { get; private set; }

        /// <summary>
        /// 最近一次业务相关性标识
        /// </summary>
        public string? LastCorrelationId { get; private set; }

        /// <summary>
        /// 最近一次实例标识
        /// </summary>
        public string? LastInstanceId { get; private set; }

        /// <summary>
        /// 最近一次挂起/取消/终止原因
        /// </summary>
        public string? LastReason { get; private set; }

        /// <summary>
        /// 最近一次取消令牌
        /// </summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>
        /// 启动流程实例
        /// </summary>
        /// <param name="request">启动请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken = default)
        {
            LastStartRequest = request;
            LastToken = cancellationToken;
            return Task.FromResult(WorkflowTestModels.CreateInstance());
        }

        /// <summary>
        /// 恢复书签
        /// </summary>
        /// <param name="bookmarkId">书签标识</param>
        /// <param name="inputs">恢复输入</param>
        /// <param name="throwIfNotResumable">不可恢复时是否抛异常</param>
        /// <param name="expectedBookmarkKey">期望的书签索引键</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> ResumeBookmarkAsync(
            string bookmarkId,
            Dictionary<string, object?>? inputs = null,
            bool throwIfNotResumable = true,
            string? expectedBookmarkKey = null,
            CancellationToken cancellationToken = default)
        {
            LastBookmarkId = bookmarkId;
            LastInputs = inputs;
            LastThrowIfNotResumable = throwIfNotResumable;
            LastExpectedBookmarkKey = expectedBookmarkKey;
            LastToken = cancellationToken;
            return Task.FromResult(WorkflowTestModels.CreateInstance());
        }

        /// <summary>
        /// 发布信号
        /// </summary>
        /// <param name="signalName">信号名称</param>
        /// <param name="payload">信号载荷</param>
        /// <param name="correlationId">业务相关性标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>恢复的书签数量</returns>
        public Task<int> PublishSignalAsync(
            string signalName,
            Dictionary<string, object?>? payload = null,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            LastSignalName = signalName;
            LastPayload = payload;
            LastCorrelationId = correlationId;
            LastToken = cancellationToken;
            return Task.FromResult(0);
        }

        /// <summary>
        /// 挂起实例
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="reason">挂起原因</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> SuspendAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
        {
            LastInstanceId = instanceId;
            LastReason = reason;
            LastToken = cancellationToken;
            var instance = WorkflowTestModels.CreateInstance();
            instance.Status = WorkflowInstanceStatus.Suspended;
            return Task.FromResult(instance);
        }

        /// <summary>
        /// 恢复被挂起的实例
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> ResumeAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            LastInstanceId = instanceId;
            LastToken = cancellationToken;
            return Task.FromResult(WorkflowTestModels.CreateInstance());
        }

        /// <summary>
        /// 取消实例
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="reason">取消原因</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> CancelAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
        {
            LastInstanceId = instanceId;
            LastReason = reason;
            LastToken = cancellationToken;
            var instance = WorkflowTestModels.CreateInstance();
            instance.Status = WorkflowInstanceStatus.Canceled;
            instance.CancellationReason = reason;
            return Task.FromResult(instance);
        }

        /// <summary>
        /// 终止实例
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="reason">终止原因</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> TerminateAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
        {
            LastInstanceId = instanceId;
            LastReason = reason;
            LastToken = cancellationToken;
            var instance = WorkflowTestModels.CreateInstance();
            instance.Status = WorkflowInstanceStatus.Terminated;
            instance.CancellationReason = reason;
            return Task.FromResult(instance);
        }

        /// <summary>
        /// 重试故障实例
        /// </summary>
        /// <param name="instanceId">实例标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例</returns>
        public Task<WorkflowInstance> RetryAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            LastInstanceId = instanceId;
            LastToken = cancellationToken;
            return Task.FromResult(WorkflowTestModels.CreateInstance());
        }
    }
}
