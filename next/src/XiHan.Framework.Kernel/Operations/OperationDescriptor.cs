// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 已登记操作的描述：契约、归属特性与请求/响应类型，以及不经反射的编解码与执行入口。
/// 协议适配器按它推导路由、生成描述并执行；具体类型化实现是 <see cref="OperationDescriptor{TOperation, TRequest, TResponse}"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class OperationDescriptor
{
    /// <summary>
    /// 创建描述。
    /// </summary>
    /// <param name="contract">契约。</param>
    /// <param name="ownerId">登记它的特性清单 ID。</param>
    /// <param name="operationType">操作实现类型。</param>
    /// <param name="requestType">请求类型。</param>
    /// <param name="responseType">响应类型。</param>
    /// <param name="schema">请求与响应的 JSON Schema；手写登记可不提供。</param>
    protected OperationDescriptor(OperationContract contract, string ownerId, Type operationType, Type requestType, Type responseType, OperationSchema? schema)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        OwnerId = ownerId;
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        Schema = schema;
    }

    /// <summary>
    /// 契约。
    /// </summary>
    public OperationContract Contract { get; }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public string Id => Contract.Id;

    /// <summary>
    /// 登记它的特性清单 ID。
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// 操作实现类型。
    /// </summary>
    public Type OperationType { get; }

    /// <summary>
    /// 请求类型。
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// 响应类型。
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// 请求与响应的 JSON Schema；没有时协议适配器只能发布无 Schema 的描述。
    /// </summary>
    public OperationSchema? Schema { get; }

    /// <summary>
    /// 把 UTF-8 JSON 载荷解码为请求对象；空载荷按 <c>{}</c> 处理；不合法返回 <see cref="KernelErrors.InvalidOperationPayload"/>。
    /// </summary>
    public abstract Result<object> DecodeRequest(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// 把响应对象编码为 UTF-8 JSON。
    /// </summary>
    public abstract ReadOnlyMemory<byte> EncodeResponse(object response);

    /// <summary>
    /// 在当前调用里执行操作：从上下文的服务作用域解析实现并调用 <c>HandleAsync</c>。请求对象必须是 <see cref="RequestType"/>。
    /// </summary>
    public abstract ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request);

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({OperationType.Name}: {RequestType.Name} -> {ResponseType.Name})";
}
