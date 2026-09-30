// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Uow.Attributes;
using XiHan.Framework.Application.Attributes;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 幂等测试用下单入参
/// </summary>
/// <param name="Sku">商品编码</param>
/// <param name="Quantity">数量</param>
public sealed record CreateOrderInput(string Sku, int Quantity);

/// <summary>
/// 幂等测试用下单结果
/// </summary>
/// <param name="OrderNo">订单序号</param>
/// <param name="Sku">商品编码</param>
public sealed record OrderResult(int OrderNo, string Sku);

/// <summary>
/// 幂等测试用带文件的表单入参
/// </summary>
/// <param name="File">上传文件</param>
public sealed record UploadForm(IFormFile File);

/// <summary>
/// 动作执行计数器，可选地挂起动作直到放行
/// </summary>
public sealed class ExecutionCounter
{
    private int _count;
    private TaskCompletionSource _gate = CreateOpenGate();

    /// <summary>
    /// 已执行次数
    /// </summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// 记一次执行并返回序号
    /// </summary>
    public int Increment()
    {
        return Interlocked.Increment(ref _count);
    }

    /// <summary>
    /// 之后的动作挂起，直到 <see cref="Open"/>
    /// </summary>
    public void Close()
    {
        _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 放行挂起的动作
    /// </summary>
    public void Open()
    {
        _gate.TrySetResult();
    }

    /// <summary>
    /// 等待放行
    /// </summary>
    public Task WaitAsync()
    {
        return _gate.Task;
    }

    private static TaskCompletionSource CreateOpenGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }
}

/// <summary>
/// 幂等测试控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/idempotency-sample")]
public class IdempotencySampleController : ControllerBase
{
    private readonly ExecutionCounter _counter;

    /// <summary>
    /// 构造函数
    /// </summary>
    public IdempotencySampleController(ExecutionCounter counter)
    {
        _counter = counter;
    }

    /// <summary>
    /// 事务型幂等下单
    /// </summary>
    [HttpPost("orders")]
    [Idempotent]
    [UnitOfWork(true)]
    public async Task<OrderResult> CreateOrderAsync([FromBody] CreateOrderInput input)
    {
        var orderNo = _counter.Increment();
        await _counter.WaitAsync();
        return new OrderResult(orderNo, input.Sku);
    }

    /// <summary>
    /// 非事务型幂等下单
    /// </summary>
    [HttpPost("orders-plain")]
    [Idempotent]
    public Task<OrderResult> CreateOrderWithoutTransactionAsync([FromBody] CreateOrderInput input)
    {
        return Task.FromResult(new OrderResult(_counter.Increment(), input.Sku));
    }

    /// <summary>
    /// 未标注幂等的下单
    /// </summary>
    [HttpPost("orders-unprotected")]
    public Task<OrderResult> CreateOrderUnprotectedAsync([FromBody] CreateOrderInput input)
    {
        return Task.FromResult(new OrderResult(_counter.Increment(), input.Sku));
    }

    /// <summary>
    /// 带文件参数的幂等接口
    /// </summary>
    [HttpPost("upload")]
    [Idempotent]
    public Task<int> UploadAsync(IFormFile file)
    {
        return Task.FromResult(_counter.Increment());
    }

    /// <summary>
    /// 带文件集合参数的幂等接口
    /// </summary>
    [HttpPost("upload-many")]
    [Idempotent]
    public Task<int> UploadManyAsync(List<IFormFile> files)
    {
        return Task.FromResult(_counter.Increment());
    }

    /// <summary>
    /// 带文件表单对象的幂等接口
    /// </summary>
    [HttpPost("upload-form")]
    [Idempotent]
    public Task<int> UploadFormAsync([FromForm] UploadForm form)
    {
        return Task.FromResult(_counter.Increment());
    }
}
