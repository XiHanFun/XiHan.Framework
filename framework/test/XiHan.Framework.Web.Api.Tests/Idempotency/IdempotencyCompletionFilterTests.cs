// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 内层幂等完成过滤器测试
/// </summary>
public class IdempotencyCompletionFilterTests
{
    /// <summary>
    /// 完成写入发生在事务提交之前，之后记录可重播
    /// </summary>
    [Fact]
    public async Task Completion_HappensBeforeCommit()
    {
        await using var fixture = new CompletionFixture();

        var run = await fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "SKU")));

        Assert.True(fixture.Store.CompleteCalled);
        Assert.False(fixture.Store.CompletedWhileTransactionCommitted);
        Assert.True(fixture.Store.CompletedWhileUnitOfWorkPresent);
        Assert.True(run.TransactionApi.Committed);
        Assert.True(run.Execution!.IsCompleted);
        var replay = await fixture.Store.TryAcquireAsync(run.Execution.Key, "fp", isTransactional: true);
        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
    }

    /// <summary>
    /// 事务型工作单元提交前同一键仍为处理中，提交后才可重播
    /// </summary>
    [Fact]
    public async Task Completion_NotVisibleAsReplayBeforeCommit()
    {
        await using var fixture = new CompletionFixture();
        fixture.Store.ProbeAfterComplete = true;

        var run = await fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "SKU")));

        Assert.Equal(IdempotencyAcquireStatus.InProgress, fixture.Store.StatusAfterComplete);
        Assert.True(run.TransactionApi.Committed);
        var replay = await fixture.Store.TryAcquireAsync(run.Execution!.Key, "fp", isTransactional: true);
        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
    }

    /// <summary>
    /// 快照按 MVC JSON 配置序列化
    /// </summary>
    [Fact]
    public async Task Snapshot_UsesMvcJsonOptions()
    {
        await using var fixture = new CompletionFixture();

        var run = await fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "SKU")) { StatusCode = 201 });

        var replay = await fixture.Store.TryAcquireAsync(run.Execution!.Key, "fp", isTransactional: true);
        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Equal(201, replay.Response!.StatusCode);
        Assert.Equal("""{"orderNo":1,"sku":"SKU"}""", Encoding.UTF8.GetString(replay.Response.Body!));
    }

    /// <summary>
    /// 空结果与纯状态码结果保存为无响应体的快照
    /// </summary>
    [Fact]
    public async Task EmptyAndStatusCodeResults_AreSnapshotWithoutBody()
    {
        await using var emptyFixture = new CompletionFixture();
        var emptyRun = await emptyFixture.ExecuteChainAsync(new EmptyResult());
        var emptyReplay = await emptyFixture.Store.TryAcquireAsync(emptyRun.Execution!.Key, "fp", isTransactional: true);
        Assert.Equal(200, emptyReplay.Response!.StatusCode);
        Assert.Null(emptyReplay.Response.Body);

        await using var statusFixture = new CompletionFixture();
        var statusRun = await statusFixture.ExecuteChainAsync(new StatusCodeResult(204));
        var statusReplay = await statusFixture.Store.TryAcquireAsync(statusRun.Execution!.Key, "fp", isTransactional: true);
        Assert.Equal(204, statusReplay.Response!.StatusCode);
        Assert.Null(statusReplay.Response.Body);
    }

    /// <summary>
    /// 快照超过单条上限时不写入完成
    /// </summary>
    [Fact]
    public async Task SnapshotTooLarge_DoesNotComplete()
    {
        await using var fixture = new CompletionFixture(options => options.MaxResponseBytes = 8);

        var run = await fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "A-VERY-LONG-SKU-VALUE")));

        Assert.False(run.Execution!.IsCompleted);
        Assert.False(fixture.Store.CompleteCalled);
        var again = await fixture.Store.TryAcquireAsync(run.Execution.Key, "fp", isTransactional: true);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, again.Status);
    }

    /// <summary>
    /// 不支持的结果类型不写入完成
    /// </summary>
    [Fact]
    public async Task UnsupportedResult_DoesNotComplete()
    {
        await using var fixture = new CompletionFixture();

        var run = await fixture.ExecuteChainAsync(new FileContentResult([1, 2, 3], "application/octet-stream"));

        Assert.False(run.Execution!.IsCompleted);
        Assert.False(fixture.Store.CompleteCalled);
        var again = await fixture.Store.TryAcquireAsync(run.Execution.Key, "fp", isTransactional: true);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, again.Status);
    }

    /// <summary>
    /// 完成写入失败时异常外抛，业务事务不提交
    /// </summary>
    [Fact]
    public async Task CompletionFailure_RollsBackBusinessTransaction()
    {
        await using var fixture = new CompletionFixture();
        fixture.Store.ThrowOnComplete = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "SKU"))));

        Assert.NotNull(fixture.LastTransactionApi);
        Assert.False(fixture.LastTransactionApi.Committed);
        Assert.True(fixture.LastTransactionApi.RolledBack);
    }

    /// <summary>
    /// 没有幂等执行上下文时直接放行
    /// </summary>
    [Fact]
    public async Task NoExecution_PassesThrough()
    {
        await using var fixture = new CompletionFixture();

        var run = await fixture.ExecuteChainAsync(new ObjectResult(new OrderResult(1, "SKU")), acquire: false);

        Assert.False(fixture.Store.CompleteCalled);
        Assert.True(run.TransactionApi.Committed);
    }

    private sealed record ChainRun(IdempotencyExecution? Execution, RecordingTransactionApi TransactionApi);

    /// <summary>
    /// 包装进程内存储，记录完成写入时的事务与工作单元状态
    /// </summary>
    private sealed class RecordingIdempotencyStore(DefaultIdempotencyStore inner, IAmbientUnitOfWork ambient) : IIdempotencyStore
    {
        public RecordingTransactionApi? TransactionApi { get; set; }

        public bool ThrowOnComplete { get; set; }

        public bool CompleteCalled { get; private set; }

        public bool CompletedWhileTransactionCommitted { get; private set; }

        public bool CompletedWhileUnitOfWorkPresent { get; private set; }

        public bool ProbeAfterComplete { get; set; }

        public IdempotencyAcquireStatus? StatusAfterComplete { get; private set; }

        public Task<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken = default)
        {
            return inner.TryAcquireAsync(key, fingerprint, isTransactional, cancellationToken);
        }

        public Task CompleteAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken = default)
        {
            CompleteCalled = true;
            CompletedWhileTransactionCommitted = TransactionApi?.Committed ?? false;
            CompletedWhileUnitOfWorkPresent = ambient.UnitOfWork is not null;
            if (ThrowOnComplete)
            {
                throw new InvalidOperationException("完成写入失败");
            }

            return CompleteAndProbeAsync(key, ownerToken, response, cancellationToken);
        }

        private async Task CompleteAndProbeAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken)
        {
            await inner.CompleteAsync(key, ownerToken, response, cancellationToken);
            if (ProbeAfterComplete)
            {
                StatusAfterComplete = (await inner.TryAcquireAsync(key, "fp", true, cancellationToken)).Status;
            }
        }

        public Task ReleaseAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
        {
            return inner.ReleaseAsync(key, ownerToken, cancellationToken);
        }

        public Task MarkIndeterminateAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
        {
            return inner.MarkIndeterminateAsync(key, ownerToken, cancellationToken);
        }
    }

    /// <summary>
    /// 串起真实工作单元过滤器、完成过滤器与模拟动作的夹具
    /// </summary>
    private sealed class CompletionFixture : IAsyncDisposable
    {
        private readonly IdempotencyFilterTestContext _context;

        public CompletionFixture(Action<XiHanIdempotencyOptions>? configure = null)
        {
            _context = new IdempotencyFilterTestContext(configure);
            Store = new RecordingIdempotencyStore(_context.Store, _context.Provider.GetRequiredService<IAmbientUnitOfWork>());
        }

        public RecordingIdempotencyStore Store { get; }

        public RecordingTransactionApi? LastTransactionApi { get; private set; }

        public async Task<ChainRun> ExecuteChainAsync(IActionResult result, bool acquire = true)
        {
            var provider = _context.Provider;
            var method = typeof(IdempotencySampleController).GetMethod(nameof(IdempotencySampleController.CreateOrderAsync))!;
            var httpContext = new DefaultHttpContext { RequestServices = provider };
            var descriptor = new ControllerActionDescriptor
            {
                MethodInfo = method,
                ControllerTypeInfo = typeof(IdempotencySampleController).GetTypeInfo(),
                ActionName = method.Name,
                ControllerName = nameof(IdempotencySampleController)
            };
            var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
            var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());

            IdempotencyExecution? execution = null;
            if (acquire)
            {
                var recordKey = new IdempotencyRecordKey("", "42", "POST", "/api/idempotency-sample/orders", "k1");
                var acquired = await Store.TryAcquireAsync(recordKey, "fp", isTransactional: true);
                execution = new IdempotencyExecution(recordKey, acquired.OwnerToken, isTransactional: true);
                httpContext.Items[IdempotencyExecution.ItemKey] = execution;
            }

            var transactionApi = new RecordingTransactionApi();
            Store.TransactionApi = transactionApi;
            LastTransactionApi = transactionApi;

            var jsonOptions = new JsonOptions();
            jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            var completionFilter = new XiHanIdempotencyCompletionFilter(
                Store,
                Microsoft.Extensions.Options.Options.Create(_context.Options),
                Microsoft.Extensions.Options.Options.Create(jsonOptions),
                NullLogger<XiHanIdempotencyCompletionFilter>.Instance);
            var unitOfWorkFilter = new XiHanUnitOfWorkFilter(provider.GetRequiredService<IUnitOfWorkManager>());
            var ambient = provider.GetRequiredService<IAmbientUnitOfWork>();

            await unitOfWorkFilter.OnActionExecutionAsync(executing, async () =>
            {
                ActionExecutedContext? executed = null;
                await completionFilter.OnActionExecutionAsync(executing, () =>
                {
                    ambient.UnitOfWork?.GetOrAddTransactionApi("test", () => transactionApi);
                    executed = new ActionExecutedContext(actionContext, [], controller: new object()) { Result = result };
                    return Task.FromResult(executed);
                });
                return executed!;
            });

            return new ChainRun(execution, transactionApi);
        }

        public ValueTask DisposeAsync()
        {
            return _context.DisposeAsync();
        }
    }
}
