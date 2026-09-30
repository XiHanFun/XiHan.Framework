// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 外层幂等过滤器测试
/// </summary>
public class IdempotencyFilterTests
{
    private static Dictionary<string, object?> Args(string sku = "SKU-1")
    {
        return new() { ["input"] = new CreateOrderInput(sku, 1) };
    }

    /// <summary>
    /// 未标注幂等的动作无需幂等键，直接放行
    /// </summary>
    [Fact]
    public async Task UnprotectedAction_RunsWithoutKey()
    {
        await using var ctx = new IdempotencyFilterTestContext();

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderUnprotectedAsync), null, Args());

        Assert.Null(result);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 未认证调用方返回 401 且不执行动作
    /// </summary>
    [Fact]
    public async Task Unauthenticated_Returns401WithoutRunningAction()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        ctx.User.IsAuthenticated = false;
        ctx.User.UserId = null;

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        AssertRejected(result, StatusCodes.Status401Unauthorized);
        Assert.Equal(0, ctx.ActionInvocations);
    }

    /// <summary>
    /// 幂等键缺失、为空或含非法字符时返回 400
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    public async Task InvalidKey_Returns400(string? key)
    {
        await using var ctx = new IdempotencyFilterTestContext();

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), key, Args());

        AssertRejected(result, StatusCodes.Status400BadRequest);
        Assert.Equal(0, ctx.ActionInvocations);
    }

    /// <summary>
    /// 幂等键超过长度上限时返回 400
    /// </summary>
    [Fact]
    public async Task KeyTooLong_Returns400()
    {
        await using var ctx = new IdempotencyFilterTestContext();

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), new string('k', 129), Args());

        AssertRejected(result, StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// 相同幂等键的第二次请求重播已保存响应且不再执行动作
    /// </summary>
    [Fact]
    public async Task SecondRequest_ReplaysWithoutRunningAction()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        var (result, http) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        var objectResult = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<JsonElement>(objectResult.Value);
        Assert.Equal(1, body.GetProperty("orderNo").GetInt32());
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal("true", http.Response.Headers[XiHanIdempotencyFilter.ReplayedHeaderName].ToString());
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 相同幂等键但请求内容不同返回 409
    /// </summary>
    [Fact]
    public async Task SameKeyDifferentContent_Returns409()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args("SKU-1"));

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args("SKU-2"));

        AssertRejected(result, StatusCodes.Status409Conflict);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 不同用户或租户使用相同幂等键互不重播
    /// </summary>
    [Fact]
    public async Task DifferentUserOrTenant_DoesNotReplayOthersResponse()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        ctx.User.UserId = 43;
        var (otherUser, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());
        ctx.User.UserId = 42;
        ctx.Tenant.Id = 7;
        var (otherTenant, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        Assert.Null(otherUser);
        Assert.Null(otherTenant);
        Assert.Equal(3, ctx.ActionInvocations);
    }

    /// <summary>
    /// 事务型动作抛异常后释放幂等键，允许重试
    /// </summary>
    [Fact]
    public async Task TransactionalActionThrew_ReleasesKeyForRetry()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(),
            action: () => throw new InvalidOperationException("业务失败"));

        var (retry, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        Assert.Null(retry);
        Assert.Equal(2, ctx.ActionInvocations);
    }

    /// <summary>
    /// 非事务型动作抛异常后标记结果不确定，重试返回 409
    /// </summary>
    [Fact]
    public async Task NonTransactionalActionThrew_MarksIndeterminate()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderWithoutTransactionAsync), "k1", Args(),
            action: () => throw new InvalidOperationException("业务失败"));

        var (retry, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderWithoutTransactionAsync), "k1", Args());

        AssertRejected(retry, StatusCodes.Status409Conflict);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 动作成功但内层未写入完成时标记结果不确定
    /// </summary>
    [Fact]
    public async Task ActionSucceededWithoutCompletion_MarksIndeterminate()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(), completeInAction: false);

        var (retry, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        AssertRejected(retry, StatusCodes.Status409Conflict);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 含文件参数的幂等接口返回 415
    /// </summary>
    [Fact]
    public async Task FileArgument_Returns415()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        var file = new FormFile(Stream.Null, 0, 0, "file", "a.txt");

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.UploadAsync), "k1",
            new Dictionary<string, object?> { ["file"] = file });

        AssertRejected(result, StatusCodes.Status415UnsupportedMediaType);
    }

    /// <summary>
    /// 文件集合与含文件属性的表单对象按参数类型返回 415 且不执行动作
    /// </summary>
    [Theory]
    [InlineData(nameof(IdempotencySampleController.UploadManyAsync), "files")]
    [InlineData(nameof(IdempotencySampleController.UploadFormAsync), "form")]
    public async Task FileParameterTypes_Return415WithoutRunningAction(string actionName, string parameterName)
    {
        await using var ctx = new IdempotencyFilterTestContext();

        var (result, _) = await ctx.ExecuteAsync(actionName, "k1",
            new Dictionary<string, object?> { [parameterName] = null });

        AssertRejected(result, StatusCodes.Status415UnsupportedMediaType);
        Assert.Equal(0, ctx.ActionInvocations);
    }

    /// <summary>
    /// 请求携带表单文件时返回 415
    /// </summary>
    [Fact]
    public async Task RequestWithFormFiles_Returns415()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        ctx.FormFiles.Add(new FormFile(Stream.Null, 0, 0, "f", "a.txt"));

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        AssertRejected(result, StatusCodes.Status415UnsupportedMediaType);
        Assert.Equal(0, ctx.ActionInvocations);
    }

    /// <summary>
    /// 参数类型判定：文件、流及含文件属性的对象为不支持
    /// </summary>
    [Theory]
    [InlineData(typeof(Stream), true)]
    [InlineData(typeof(IFormFile), true)]
    [InlineData(typeof(IFormFile[]), true)]
    [InlineData(typeof(List<IFormFile>), true)]
    [InlineData(typeof(IFormFileCollection), true)]
    [InlineData(typeof(UploadForm), true)]
    [InlineData(typeof(CreateOrderInput), false)]
    [InlineData(typeof(string), false)]
    public void IsUnsupportedParameterType_ClassifiesTypes(Type type, bool expected)
    {
        Assert.Equal(expected, RequestFingerprint.IsUnsupportedParameterType(type));
    }

    /// <summary>
    /// 参数序列化后超过上限返回 413
    /// </summary>
    [Fact]
    public async Task ArgumentsTooLarge_Returns413()
    {
        await using var ctx = new IdempotencyFilterTestContext(options => options.MaxRequestBytes = 16);

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(new string('x', 64)));

        AssertRejected(result, StatusCodes.Status413PayloadTooLarge);
    }

    /// <summary>
    /// CancellationToken 参数不参与请求摘要
    /// </summary>
    [Fact]
    public async Task CancellationTokenArgument_IsExcludedFromFingerprint()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        var first = Args();
        first["cancellationToken"] = new CancellationTokenSource().Token;
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", first);

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args());

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 路径大小写不同的相同请求视为同一记录并重播
    /// </summary>
    [Fact]
    public async Task PathCaseInsensitive_Replays()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(), path: "/api/Orders");

        var (result, http) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(), path: "/api/orders");

        Assert.IsType<ObjectResult>(result);
        Assert.Equal("true", http.Response.Headers[XiHanIdempotencyFilter.ReplayedHeaderName].ToString());
        Assert.Equal(1, ctx.ActionInvocations);
    }

    /// <summary>
    /// 表单内容无法读取时返回 400 且不执行动作
    /// </summary>
    [Fact]
    public async Task MalformedForm_Returns400()
    {
        await using var ctx = new IdempotencyFilterTestContext();

        var (result, _) = await ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(),
            configureRequest: request =>
            {
                request.ContentType = "multipart/form-data; boundary=x";
                request.Body = new MemoryStream("not a multipart body"u8.ToArray());
            });

        AssertRejected(result, StatusCodes.Status400BadRequest);
        Assert.Equal(0, ctx.ActionInvocations);
    }

    /// <summary>
    /// 收尾释放失败时动作原本的异常保持不变
    /// </summary>
    [Fact]
    public async Task ReleaseFailure_KeepsOriginalException()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        ctx.FilterStore = new FailingCleanupStore(ctx.Store);
        var original = new InvalidOperationException("业务失败");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderAsync), "k1", Args(),
                action: () => throw original, throwFromNext: true));

        Assert.Same(original, thrown);
    }

    /// <summary>
    /// 收尾标记不确定失败时过滤器正常返回，执行结果中的原异常保持不变
    /// </summary>
    [Fact]
    public async Task MarkIndeterminateFailure_DoesNotReplaceExecutedException()
    {
        await using var ctx = new IdempotencyFilterTestContext();
        ctx.FilterStore = new FailingCleanupStore(ctx.Store);

        var exception = await Record.ExceptionAsync(() =>
            ctx.ExecuteAsync(nameof(IdempotencySampleController.CreateOrderWithoutTransactionAsync), "k1", Args(),
                action: () => throw new InvalidOperationException("业务失败")));

        Assert.Null(exception);
        Assert.Equal(1, ctx.ActionInvocations);
    }

    private static void AssertRejected(IActionResult? result, int statusCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        Assert.IsType<ApiResponse>(objectResult.Value);
    }

    /// <summary>
    /// 释放与标记不确定一律抛出异常的存储
    /// </summary>
    private sealed class FailingCleanupStore(IIdempotencyStore inner) : IIdempotencyStore
    {
        public Task<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken = default)
        {
            return inner.TryAcquireAsync(key, fingerprint, isTransactional, cancellationToken);
        }

        public Task CompleteAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken = default)
        {
            return inner.CompleteAsync(key, ownerToken, response, cancellationToken);
        }

        public Task ReleaseAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("释放失败");
        }

        public Task MarkIndeterminateAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("标记不确定失败");
        }
    }
}
