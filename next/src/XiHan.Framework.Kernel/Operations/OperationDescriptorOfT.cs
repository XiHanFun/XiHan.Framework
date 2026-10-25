// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 类型化的操作描述：用源生成的 <see cref="JsonTypeInfo{T}"/> 编解码载荷，从作用域容器解析 <typeparamref name="TOperation"/> 执行；全程不经反射，可 AOT。
/// </summary>
/// <typeparam name="TOperation">操作实现。</typeparam>
/// <typeparam name="TRequest">请求类型。</typeparam>
/// <typeparam name="TResponse">响应类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OperationDescriptor<TOperation, TRequest, TResponse> : OperationDescriptor
    where TOperation : class, IOperation<TRequest, TResponse>
{
    /// <summary>
    /// 空载荷视为空对象，让没有输入的操作（如 <c>orders.list</c>）可以不带请求体。
    /// </summary>
    private static readonly byte[] _emptyObject = "{}"u8.ToArray();

    /// <summary>
    /// 请求类型的序列化元数据。
    /// </summary>
    private readonly JsonTypeInfo<TRequest> _requestInfo;

    /// <summary>
    /// 响应类型的序列化元数据。
    /// </summary>
    private readonly JsonTypeInfo<TResponse> _responseInfo;

    /// <summary>
    /// 创建描述。
    /// </summary>
    /// <param name="contract">契约。</param>
    /// <param name="ownerId">登记它的特性清单 ID。</param>
    /// <param name="requestInfo">请求类型的序列化元数据（源生成）。</param>
    /// <param name="responseInfo">响应类型的序列化元数据（源生成）。</param>
    /// <param name="schema">请求与响应的 JSON Schema；手写登记可不提供。</param>
    public OperationDescriptor(OperationContract contract, string ownerId, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, OperationSchema? schema = null)
        : base(contract, ownerId, typeof(TOperation), typeof(TRequest), typeof(TResponse), schema)
    {
        _requestInfo = requestInfo ?? throw new ArgumentNullException(nameof(requestInfo));
        _responseInfo = responseInfo ?? throw new ArgumentNullException(nameof(responseInfo));
    }

    /// <inheritdoc />
    public override JsonTypeInfo RequestInfo => _requestInfo;

    /// <inheritdoc />
    public override JsonTypeInfo ResponseInfo => _responseInfo;

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var request = JsonSerializer.Deserialize(payload.IsEmpty ? _emptyObject : payload.Span, _requestInfo);
            // JSON 字面量 null 不是请求对象：按载荷不合法处理，不让空请求进入操作
            return request is null
                ? InvalidPayload("the payload is null")
                : Result.Success<object>(request);
        }
        catch (JsonException exception)
        {
            return InvalidPayload(exception.Message);
        }
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonSerializer.SerializeToUtf8Bytes((TResponse)response, _responseInfo);
    }

    /// <inheritdoc />
    public override async ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (request is not TRequest typed)
        {
            return new Error(KernelErrors.OperationTypeMismatch, KernelMessages.OperationTypeMismatch, MessageArguments.Of(("id", Id), ("expected", typeof(TRequest).Name), ("actual", request.GetType().Name)));
        }

        // 操作按作用域解析：每次调用一个作用域，操作里的仓储、工作单元等作用域服务与中间件共享同一实例
        var operation = context.Services.GetRequiredService<TOperation>();
        var result = await operation.HandleAsync(typed, context).ConfigureAwait(false);
        return result.TryGetValue(out var value) ? Result.Success<object>(value!) : Result.Failure<object>(result.Error!);
    }

    /// <summary>
    /// 构造载荷不合法的失败结果。
    /// </summary>
    private Result<object> InvalidPayload(string reason)
        => Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", Id), ("reason", reason)));
}
