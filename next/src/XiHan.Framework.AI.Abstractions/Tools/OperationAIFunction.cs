// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tools;

/// <summary>
/// 把一个操作契约桥接成模型工具
/// </summary>
/// <remarks>
/// 参数序列化成请求 JSON（无法序列化的参数以 <c>AI_VALIDATION_001</c> 交给模型），在新作用域的新上下文里经完整管道执行。新上下文复制调用方的追踪 ID、Baggage、截止时间与安全上下文（主体与租户），工具以调用方身份执行、Authorize 阶段照常按契约判定；凭据不复制，不重新认证。成功返回响应 JSON，失败返回 <c>{"error","message"}</c> 交给模型
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OperationAIFunction : AIFunction
{
    /// <summary>
    /// 操作
    /// </summary>
    private readonly OperationDescriptor _descriptor;

    /// <summary>
    /// 调用器
    /// </summary>
    private readonly IOperationInvoker _invoker;

    /// <summary>
    /// 作用域工厂
    /// </summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// 本地化器（可无）
    /// </summary>
    private readonly IMessageLocalizer? _localizer;

    /// <summary>
    /// 调用方上下文
    /// </summary>
    private readonly InvocationContext _caller;

    /// <summary>
    /// 创建（由 <see cref="AIToolCatalog"/> 在绑定时调用）
    /// </summary>
    internal OperationAIFunction(string name, string? description, JsonElement schema, JsonElement? returnSchema, OperationDescriptor descriptor, IOperationInvoker invoker, IServiceScopeFactory scopes, IMessageLocalizer? localizer, InvocationContext caller)
    {
        Name = name;
        Description = description ?? string.Empty;
        JsonSchema = schema;
        ReturnJsonSchema = returnSchema;
        _descriptor = descriptor;
        _invoker = invoker;
        _scopes = scopes;
        _localizer = localizer;
        _caller = caller;
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public override JsonElement JsonSchema { get; }

    /// <inheritdoc />
    public override JsonElement? ReturnJsonSchema { get; }

    /// <summary>
    /// 桥接的操作 ID
    /// </summary>
    public string OperationId => _descriptor.Id;

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        // 每次工具调用都是一次独立调用：新作用域、新上下文，Authorize 阶段按契约判定；Baggage 与截止时间沿用调用方，追踪 ID 不变
        await using var scope = _scopes.CreateAsyncScope();
        var context = App.CreateContext(
            scope.ServiceProvider,
            cancellationToken,
            traceId: _caller.TraceId,
            deadline: _caller.Deadline,
            baggage: new Dictionary<string, string>(_caller.Baggage, StringComparer.Ordinal));
        // 以调用方身份执行：主体与租户取调用方此刻的安全上下文（调用方已在自己的管道里认证过）。不复制凭据，Authenticate 阶段没有凭据可认证即保留这个主体，
        // Authorize 阶段以它按契约判定——工具既不能绕过授权，也不会因为丢了身份被当成匿名一律拒绝
        context.SetSecurity(_caller.GetSecurity());
        // 模型给的参数序列化不了，与操作失败一样以 JSON 错误交给模型，不抛异常
        var payload = SerializeArguments(arguments);
        if (!payload.TryGetValue(out var body))
        {
            return Failure(payload.Error!, context);
        }

        var response = await _invoker.InvokeAsync(context, new OperationRequest(_descriptor.Id, body)).ConfigureAwait(false);
        if (response.IsSuccess)
        {
            if (response.Payload.Length == 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(response.Payload);
            return document.RootElement.Clone();
        }

        return Failure(response.Error!, context);
    }

    /// <summary>
    /// 交给模型的错误对象 <c>{"error","message"}</c>
    /// </summary>
    /// <remarks>
    /// 文案按调用方区域翻译，没有本地化器时取错误的描述
    /// </remarks>
    private JsonElement Failure(Error error, InvocationContext context)
    {
        var text = _localizer is not null ? _localizer.Localize(error, context) : error.Message;
        using var failure = JsonDocument.Parse(new JsonObject { ["error"] = error.Code, ["message"] = text }.ToJsonString());
        return failure.RootElement.Clone();
    }

    /// <summary>
    /// 参数 → 请求 JSON
    /// </summary>
    /// <remarks>
    /// JSON 元素与节点原样写入，标量按类型写入，其他类型按序列化选项里登记的类型信息写入；序列化不了的参数返回 <c>AI_VALIDATION_001</c>
    /// </remarks>
    private Result<ReadOnlyMemory<byte>> SerializeArguments(AIFunctionArguments arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in arguments)
            {
                writer.WritePropertyName(name);
                if (!WriteValue(writer, value))
                {
                    return Error.Validation(AIErrors.RequestInvalid, AIMessages.ToolArgumentInvalid, MessageArguments.Of(("name", Name), ("argument", name)));
                }
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// 写一个参数值
    /// </summary>
    /// <remarks>
    /// 序列化选项里没有该类型的信息、或序列化本身失败时返回 <see langword="false"/>
    /// </remarks>
    private bool WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case JsonNode node:
                node.WriteTo(writer);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(moment);
                break;
            case DateTime moment:
                writer.WriteStringValue(moment);
                break;
            case Guid id:
                writer.WriteStringValue(id);
                break;
            case Enum enumeration:
                writer.WriteStringValue(enumeration.ToString());
                break;
            default:
                try
                {
                    JsonSerializer.Serialize(writer, value, JsonSerializerOptions.GetTypeInfo(value.GetType()));
                }
                catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
                {
                    return false;
                }

                break;
        }

        return true;
    }
}
