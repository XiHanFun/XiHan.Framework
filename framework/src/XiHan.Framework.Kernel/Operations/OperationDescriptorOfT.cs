// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 类型化的操作描述
/// </summary>
/// <remarks>
/// 用源生成的 <see cref="JsonTypeInfo{T}"/> 编解码载荷，从作用域容器解析 <typeparamref name="TOperation"/> 执行；全程不经反射，可 AOT
/// </remarks>
/// <typeparam name="TOperation">操作实现</typeparam>
/// <typeparam name="TRequest">请求类型</typeparam>
/// <typeparam name="TResponse">响应类型</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OperationDescriptor<TOperation, TRequest, TResponse> : OperationDescriptor
    where TOperation : class, IOperation<TRequest, TResponse>
{
    /// <summary>
    /// 空载荷视为空对象，让没有输入的操作（如 <c>orders.list</c>）可以不带请求体
    /// </summary>
    private static readonly byte[] _emptyObject = "{}"u8.ToArray();

    /// <summary>
    /// 请求类型的序列化元数据
    /// </summary>
    private readonly JsonTypeInfo<TRequest> _requestInfo;

    /// <summary>
    /// 响应类型的序列化元数据
    /// </summary>
    private readonly JsonTypeInfo<TResponse> _responseInfo;

    /// <summary>
    /// 创建描述
    /// </summary>
    /// <remarks>
    /// <c>schema</c> 取 <c>null</c>
    /// </remarks>
    /// <param name="contract">契约</param>
    /// <param name="ownerId">登记它的特性清单 ID</param>
    /// <param name="requestInfo">请求类型的序列化元数据（源生成）</param>
    /// <param name="responseInfo">响应类型的序列化元数据（源生成）</param>
    public OperationDescriptor(OperationContract contract, string ownerId, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo)
        : this(contract, ownerId, requestInfo, responseInfo, null)
    {
    }

    /// <summary>
    /// 创建描述
    /// </summary>
    /// <param name="contract">契约</param>
    /// <param name="ownerId">登记它的特性清单 ID</param>
    /// <param name="requestInfo">请求类型的序列化元数据（源生成）</param>
    /// <param name="responseInfo">响应类型的序列化元数据（源生成）</param>
    /// <param name="schema">请求与响应的 JSON Schema；手写登记可不提供</param>
    public OperationDescriptor(OperationContract contract, string ownerId, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, OperationSchema? schema)
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
        var json = payload.IsEmpty ? _emptyObject : payload;

        // 先过共同的读取检查：语法错误一律写 $，重复成员写它的路径（RFC-0011 规则 2，ADR-0236）
        if (JsonText.FindProblem(json.Span).TryGetValue(out var problem))
        {
            return InvalidPayload(problem.Path);
        }

        using var document = JsonDocument.Parse(json);

        // 再按序列化元数据以规范的顺序核对形状：System.Text.Json 报哪一处随类型的声明方式而变，这里不随
        if (JsonRequestShape.FindProblem(document.RootElement, _requestInfo) is { } path)
        {
            return InvalidPayload(path);
        }

        try
        {
            var request = document.RootElement.Deserialize(_requestInfo);
            // JSON 字面量 null 不是请求对象：按载荷不合法处理，不让空请求进入操作；reason 与其他载荷错误一样只写路径（RFC-0011 规则 2，ADR-0235）
            return request is null
                ? InvalidPayload(JsonPath.Root)
                : Result.Success<object>(request);
        }
        catch (JsonException exception)
        {
            // 只留路径：消息带目标类型的全名，这个错误要回给调用方（ADR-0124）。读取器的错误（序列化上下文把 MaxDepth 设得更小时）与语法错误同样写 $
            return InvalidPayload(exception.InnerException is JsonException ? JsonPath.Root : exception.Path ?? JsonPath.Root);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 写不成合法的 JSON 文本只抛 <see cref="ArgumentException"/>，与浮点成员是 NaN 时一样由 XGI 入口按未处理的异常结束、不带 <c>inner</c>（RFC-0011 规则 10「编码」）：
    /// 响应里的字符串有孤立的代理项（以 UTF-8 交来的不是合法的 UTF-8），不像 System.Text.Json 的缺省那样写成转义的 U+FFFD（ADR-0318）；写出的整段文本没过 <see cref="JsonText.FindProblem"/>，即嵌套超过 64 层、有重复成员或孤立代理项的转义（ADR-0319）；
    /// 超过写出深度时 System.Text.Json 抛出的 <see cref="JsonException"/> 也包成 <see cref="ArgumentException"/>，原异常在 <see cref="Exception.InnerException"/>（2026-11-27 起，ADR-0320；以前原样抛出 <see cref="JsonException"/>）。
    /// 交出的字节与 <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonTypeInfo{TValue})"/> 写出的相同；文本里有 <c>\uFFFD</c> 或序列化上下文用了自定义的编码器时，另写一遍只为核对。
    /// 几处有意维持的边角（ADR-0318「修订」、ADR-0320）：
    /// 不经编码器原样写出的 <c>\uFFFD</c> 转义（转换器以 <c>WriteRawValue</c> 写的文本、预先编码的 <see cref="System.Text.Json.JsonEncodedText"/>、缺省编码器下名字里有 U+FFFD 的属性名）在核对的那一遍里仍是转义，被当成写不出；
    /// 序列化上下文的 <see cref="JsonSerializerOptions.MaxDepth"/> 比 64 小时写出照它，与按同一个上下文读请求的层数相同；
    /// 写出先快后慢（2026-11-27 起，ADR-0342）：先照序列化上下文自己的元数据写，选项与生成时一致时走源生成的快速路径；快速路径写不出 64 层、最里层是可空值的响应（里面再调序列化器时按原选项的上限），
    /// 更深的由写出器抛出 <see cref="InvalidOperationException"/>，只在这两种情形下向解析器为改了深度上限的选项副本重新取元数据、按元数据再写一遍（64 层照写，更深的也只抛 <see cref="ArgumentException"/>，ADR-0329）。
    /// 只认原选项的自定义解析器在要再写一遍时给不出元数据，异常原样抛出；只生成快速路径（<c>GenerationMode = Serialization</c>）的上下文照常写出（ADR-0329 到 ADR-0342 之间第一次写出即失败），
    /// 要再写一遍时同样给不出，抛出解析器的 <see cref="InvalidOperationException"/>，XGI 入口照旧按未处理的异常结束
    /// </remarks>
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // 整段文本过共同检查：转换器、原样嵌进去的文本、命名策略与扩展数据都可能写出超过 64 层、有重复成员或孤立代理项转义的文本，读的一方都会拒绝（ADR-0319）；
        // 被写出器换成 U+FFFD 的字符这道检查看不出，另外核对
        return Write((TResponse)response, _responseInfo, checkText: true);
    }

    /// <inheritdoc />
    public override async ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (request is not TRequest typed)
        {
            // 只带操作 ID：类型名是实现语言的，各语言写不出同样的参数（RFC-0011「错误码」，ADR-0236）
            return new Error(KernelErrors.OperationTypeMismatch, KernelMessages.OperationTypeMismatch, MessageArguments.Of(("id", Id)));
        }

        // 操作按作用域解析：每次调用一个作用域，操作里的仓储、工作单元等作用域服务与中间件共享同一实例
        var operation = context.Services.GetRequiredService<TOperation>();
        var result = await operation.HandleAsync(typed, context).ConfigureAwait(false);
        return result.TryGetValue(out var value) ? Result.Success<object>(value!) : Result.Failure<object>(result.Error!);
    }
}
