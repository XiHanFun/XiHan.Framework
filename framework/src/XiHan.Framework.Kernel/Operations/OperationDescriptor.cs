// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 已登记操作的描述
/// </summary>
/// <remarks>
/// 契约、归属特性与请求/响应类型，以及不经反射的编解码与执行入口。协议适配器按它推导路由、生成描述并执行；具体类型化实现是 <see cref="OperationDescriptor{TOperation, TRequest, TResponse}"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class OperationDescriptor
{
    /// <summary>
    /// 各序列化选项写出用的副本
    /// </summary>
    /// <remarks>
    /// 按原选项缓存：同一个序列化上下文的描述共用一份副本，副本里的元数据只建一次；原选项回收时副本随之回收
    /// </remarks>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _writeOptions = new();

    /// <summary>
    /// 创建描述
    /// </summary>
    /// <param name="contract">契约</param>
    /// <param name="ownerId">登记它的特性清单 ID</param>
    /// <param name="operationType">操作实现类型</param>
    /// <param name="requestType">请求类型</param>
    /// <param name="responseType">响应类型</param>
    /// <param name="schema">请求与响应的 JSON Schema；手写登记可不提供</param>
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
    /// 契约
    /// </summary>
    public OperationContract Contract { get; }

    /// <summary>
    /// 操作 ID
    /// </summary>
    public string Id => Contract.Id;

    /// <summary>
    /// 登记它的特性清单 ID
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// 操作实现类型
    /// </summary>
    public Type OperationType { get; }

    /// <summary>
    /// 请求类型
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// 响应类型
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// 请求与响应的 JSON Schema
    /// </summary>
    /// <remarks>
    /// 没有时协议适配器只能发布无 Schema 的描述
    /// </remarks>
    public OperationSchema? Schema { get; }

    /// <summary>
    /// 请求类型的序列化元数据
    /// </summary>
    /// <remarks>
    /// 协议适配器据此了解请求形状（如是否声明 <c>id</c>），不经反射；载荷原样搬运、没有类型化元数据的描述（插件代理）为 null，适配器改看 <see cref="Schema"/>
    /// </remarks>
    public virtual JsonTypeInfo? RequestInfo => null;

    /// <summary>
    /// 响应类型的序列化元数据
    /// </summary>
    /// <remarks>
    /// 没有类型化元数据的描述为 null
    /// </remarks>
    public virtual JsonTypeInfo? ResponseInfo => null;

    /// <summary>
    /// 把 UTF-8 JSON 载荷解码为请求对象
    /// </summary>
    /// <remarks>
    /// 空载荷按 <c>{}</c> 处理；不合法返回 <see cref="KernelErrors.InvalidOperationPayload"/>
    /// </remarks>
    public abstract Result<object> DecodeRequest(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// 把响应对象编码为 UTF-8 JSON
    /// </summary>
    /// <remarks>
    /// 写不成合法的 JSON 文本只抛 <see cref="ArgumentException"/>（RFC-0011 规则 10「编码」）：类型化的实现在超过写出深度时也把 System.Text.Json 的 <see cref="JsonException"/> 包在 <see cref="Exception.InnerException"/> 里（ADR-0320）；
    /// 序列化器或转换器抛出的其他异常（框架异常、不支持的类型）照原样抛出，XGI 入口一并按未处理的异常结束
    /// </remarks>
    /// <exception cref="ArgumentException">响应写不成合法的 JSON 文本</exception>
    public abstract ReadOnlyMemory<byte> EncodeResponse(object response);

    /// <summary>
    /// 把 <see cref="EncodeResponse"/> 编出的 UTF-8 JSON 解码回响应对象
    /// </summary>
    /// <remarks>
    /// 幂等回放（RFC-0006）用它还原记录的响应。缺省按 <see cref="ResponseInfo"/> 解码，不经反射；没有类型化元数据、JSON 不合法或为字面量 null 时返回 <see cref="KernelErrors.InvalidOperationPayload"/>。载荷原样搬运的描述（插件代理）覆写它
    /// </remarks>
    public virtual Result<object> DecodeResponse(ReadOnlyMemory<byte> payload)
    {
        if (ResponseInfo is not { } info)
        {
            return InvalidPayload("the operation has no typed response metadata");
        }

        try
        {
            // JSON 字面量 null 不是响应对象：操作的成功值不能为 null
            return JsonSerializer.Deserialize(payload.Span, info) is { } response
                ? Result.Success(response)
                : InvalidPayload("the response is null");
        }
        catch (JsonException exception)
        {
            return InvalidPayload(exception.Message);
        }
    }

    /// <summary>
    /// 在当前调用里执行操作
    /// </summary>
    /// <remarks>
    /// 从上下文的服务作用域解析实现并调用 <c>HandleAsync</c>。请求对象必须是 <see cref="RequestType"/>
    /// </remarks>
    public abstract ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request);

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({OperationType.Name}: {RequestType.Name} -> {ResponseType.Name})";

    /// <summary>
    /// 严格写出响应或为 Schema 校验编码请求
    /// </summary>
    /// <remarks>
    /// 先快后慢（ADR-0342）：先照原选项的元数据写，选项与生成时一致的源生成上下文走快速路径，只生成快速路径（<c>GenerationMode = Serialization</c>）的上下文也写得出。
    /// 快速路径不按放宽一层的上限核对深度，只在它写不出的两种异常上照 <see cref="WriteOptions"/> 的副本按元数据再写一遍，结果以这一遍为准：
    /// 里面再调序列化器时按原选项的上限抛出的 <see cref="JsonException"/>（64 层、最里层是可空值，<see cref="StrictJson.Serialize"/> 包成 <see cref="ArgumentException"/>），与写出器按原上限抛出的 <see cref="InvalidOperationException"/>（更深的）。
    /// 字符串被换成 U+FFFD、文本没过检查、NaN 这类两遍结果相同的不再写一遍。原选项的深度上限大于 64 时直接按副本写：照原选项会写到它的上限才由文本检查拒绝，不再是 System.Text.Json 在第 65 层抛出的异常
    /// </remarks>
    /// <param name="value">响应或请求</param>
    /// <param name="info">原选项下的序列化元数据</param>
    /// <param name="checkText">是否核对整段文本，见 <see cref="StrictJson.Serialize"/></param>
    /// <exception cref="ArgumentException">写不成合法的 JSON 文本</exception>
    internal static byte[] Write(object? value, JsonTypeInfo info, bool checkText)
    {
        if (info.Options.MaxDepth > JsonText.MaxDepth)
        {
            return StrictJson.Serialize(value, WriteOptions(info.Options).GetTypeInfo(info.Type), checkText);
        }

        try
        {
            return StrictJson.Serialize(value, info, checkText);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException { InnerException: JsonException })
        {
            // 快速路径写不出不等于写不出：按元数据再写一遍，副本多给的一层让 64 层、最里层是可空值的照写，更深的同样只抛 ArgumentException；
            // 转换器自己抛出的这两种异常在这一遍里照样抛出，结果不变
            return StrictJson.Serialize(value, WriteOptions(info.Options).GetTypeInfo(info.Type), checkText);
        }
    }

    /// <summary>
    /// 按元数据写出用的序列化选项
    /// </summary>
    /// <remarks>
    /// 原选项的副本，把深度上限改成原上限与 64 取小再加 1：System.Text.Json 写到上限那一层的值就失败，读取却读得进同样层数的文本。
    /// 多给的一层由写完之后的 JSON 文本检查卡住，层数从响应或请求的根算起（RFC-0001「JSON 文本」，ADR-0319、ADR-0329）；写出用的元数据要按副本向同一个解析器重新取，源生成的上下文也一样。
    /// 原选项没有编码器时副本设成缺省的编码器，写出的字节不变：源生成的快速路径只在选项与生成时一致才用，它不按副本核对深度，编码器不为空即走按元数据的写法，深度照副本的上限（ADR-0329）。
    /// 只在 <see cref="Write"/> 的第一遍写不出时才用（ADR-0342）
    /// </remarks>
    /// <param name="options">响应或请求类型的序列化选项</param>
    internal static JsonSerializerOptions WriteOptions(JsonSerializerOptions options)
        => _writeOptions.GetValue(options, static o => new JsonSerializerOptions(o)
        {
            MaxDepth = Math.Min(o.MaxDepth == 0 ? JsonText.MaxDepth : o.MaxDepth, JsonText.MaxDepth) + 1,
            Encoder = o.Encoder ?? JavaScriptEncoder.Default,
        });

    /// <summary>
    /// 这个操作的载荷不合法的错误
    /// </summary>
    /// <remarks>
    /// 解码请求与响应、Validate 阶段为 Schema 校验编码请求共用
    /// </remarks>
    /// <param name="reason">出错的路径或原因</param>
    internal Error InvalidPayload(string reason)
        => Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", Id), ("reason", reason)));
}
