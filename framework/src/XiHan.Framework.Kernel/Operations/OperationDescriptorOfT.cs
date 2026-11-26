// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Encodings.Web;
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
    /// 响应写不出时异常的消息
    /// </summary>
    private const string UnwritableMessage = "The response cannot be written as JSON text: a string has a lone surrogate or invalid UTF-8, the text nests deeper than 64 levels, or an object has a duplicate member.";

    /// <summary>
    /// 请求类型的序列化元数据
    /// </summary>
    private readonly JsonTypeInfo<TRequest> _requestInfo;

    /// <summary>
    /// 响应类型的序列化元数据
    /// </summary>
    private readonly JsonTypeInfo<TResponse> _responseInfo;

    /// <summary>
    /// 写出响应用的序列化元数据
    /// </summary>
    /// <remarks>
    /// 按 <see cref="OperationDescriptor.ResponseWriteOptions"/> 的选项向同一个解析器重新取得，第一次写出时才取；并发时可能取两次，结果相同
    /// </remarks>
    private JsonTypeInfo<TResponse>? _writeInfo;

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
    /// 写不成合法的 JSON 文本即抛出 <see cref="ArgumentException"/>，与浮点成员是 NaN 时一样由 XGI 入口按未处理的异常结束（RFC-0011 规则 10「编码」）：
    /// 响应里的字符串有孤立的代理项（以 UTF-8 交来的不是合法的 UTF-8），不像 System.Text.Json 的缺省那样写成转义的 U+FFFD（ADR-0318）；写出的整段文本没过 <see cref="JsonText.FindProblem"/>，即嵌套超过 64 层、有重复成员或孤立代理项的转义（ADR-0319）。
    /// 交出的字节与 <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonTypeInfo{TValue})"/> 写出的相同；文本里有 <c>\uFFFD</c> 或序列化上下文用了自定义的编码器时，另写一遍只为核对
    /// </remarks>
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var value = (TResponse)response;
        var info = _writeInfo ??= (JsonTypeInfo<TResponse>)ResponseWriteOptions(_responseInfo.Options).GetTypeInfo(typeof(TResponse));
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, info);

        // 整段文本过共同检查：转换器、原样嵌进去的文本、命名策略与扩展数据都可能写出超过 64 层、有重复成员或孤立代理项转义的文本，读的一方都会拒绝（ADR-0319）
        // 被写出器换成 U+FFFD 的字符这道检查看不出，另外核对
        return JsonText.FindProblem(payload).HasValue || HasReplacedCharacter(value, info, payload) ? throw new ArgumentException(UnwritableMessage) : payload;
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

    /// <summary>
    /// 写出时是否有字符被换成了 U+FFFD
    /// </summary>
    /// <remarks>
    /// 写出器把孤立的代理项与非法的 UTF-8 换成 U+FFFD 照写，得到的仍是合法的文本。内置的编码器总把换出来的写成 <c>\uFFFD</c>，文本里没有这个转义就没有换过的字符；
    /// 有，就换不转义非 ASCII 的内置编码器再写一遍分辨：本来就是 U+FFFD 的字符原样写成 <c>EF BF BD</c>，换出来的仍是 <c>\uFFFD</c>。这一遍只看不交
    /// </remarks>
    /// <param name="value">响应对象</param>
    /// <param name="info">写出用的序列化元数据</param>
    /// <param name="payload">已经写出的文本</param>
    private static bool HasReplacedCharacter(TResponse value, JsonTypeInfo<TResponse> info, byte[] payload)
    {
        // 自定义的编码器不在此列（实测有的不报需要转义，写出器就把孤立的代理项连同后面的字符截掉、不留痕迹），总要再写一遍
        var encoder = info.Options.Encoder;
        if (payload.AsSpan().IndexOf("\\uFFFD"u8) < 0 && (encoder is null || encoder.GetType() == JavaScriptEncoder.Default.GetType()))
        {
            return false;
        }

        var check = new ArrayBufferWriter<byte>(payload.Length);
        using (var writer = new Utf8JsonWriter(check, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = info.Options.MaxDepth, SkipValidation = true }))
        {
            JsonSerializer.Serialize(writer, value, info);
        }

        return check.WrittenSpan.IndexOf("\\uFFFD"u8) >= 0;
    }

    /// <summary>
    /// 构造载荷不合法的失败结果
    /// </summary>
    private Result<object> InvalidPayload(string reason)
        => Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", Id), ("reason", reason)));
}
