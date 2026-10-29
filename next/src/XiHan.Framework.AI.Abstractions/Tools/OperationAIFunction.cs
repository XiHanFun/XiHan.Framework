// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tools;

/// <summary>
/// 把一个操作契约桥接成模型工具。
/// </summary>
/// <remarks>
/// 参数序列化成请求 JSON，在新作用域、复制了调用方 Baggage 与截止时间的新上下文里经完整管道执行；成功返回响应 JSON，失败返回 <c>{"error","message"}</c> 交给模型。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OperationAIFunction : AIFunction
{
    /// <summary>
    /// 操作。
    /// </summary>
    private readonly OperationDescriptor _descriptor;

    /// <summary>
    /// 调用器。
    /// </summary>
    private readonly IOperationInvoker _invoker;

    /// <summary>
    /// 作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// 本地化器（可无）。
    /// </summary>
    private readonly IMessageLocalizer? _localizer;

    /// <summary>
    /// 调用方上下文。
    /// </summary>
    private readonly InvocationContext _caller;

    /// <summary>
    /// 创建（由 <see cref="AIToolCatalog"/> 在绑定时调用）。
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
    /// 桥接的操作 ID。
    /// </summary>
    public string OperationId => _descriptor.Id;

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var payload = SerializeArguments(arguments);
        // 每次工具调用都是一次独立调用：新作用域、新上下文，Authorize 阶段按契约判定；Baggage 与截止时间沿用调用方，追踪 ID 不变
        await using var scope = _scopes.CreateAsyncScope();
        var context = App.CreateContext(
            scope.ServiceProvider,
            cancellationToken,
            traceId: _caller.TraceId,
            deadline: _caller.Deadline,
            baggage: new Dictionary<string, string>(_caller.Baggage, StringComparer.Ordinal));
        var response = await _invoker.InvokeAsync(context, new OperationRequest(_descriptor.Id, payload)).ConfigureAwait(false);
        if (response.IsSuccess)
        {
            if (response.Payload.Length == 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(response.Payload);
            return document.RootElement.Clone();
        }

        var error = response.Error!;
        var text = _localizer is not null ? _localizer.Localize(error, context) : error.Message;
        using var failure = JsonDocument.Parse(new JsonObject { ["error"] = error.Code, ["message"] = text }.ToJsonString());
        return failure.RootElement.Clone();
    }

    /// <summary>
    /// 参数 → 请求 JSON。
    /// </summary>
    /// <remarks>
    /// JSON 元素与节点原样写入，标量按类型写入，其他类型按序列化选项里登记的类型信息写入。
    /// </remarks>
    private ReadOnlyMemory<byte> SerializeArguments(AIFunctionArguments arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in arguments)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, name, value);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// 写一个参数值。
    /// </summary>
    private void WriteValue(Utf8JsonWriter writer, string name, object? value)
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
                var type = value.GetType();
                try
                {
                    JsonSerializer.Serialize(writer, value, JsonSerializerOptions.GetTypeInfo(type));
                }
                catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
                {
                    throw new FrameworkException(AIErrors.RequestInvalid, string.Format(CultureInfo.InvariantCulture, "Argument '{0}' of AI tool '{1}' has type '{2}', which cannot be serialized.", name, Name, type), exception);
                }

                break;
        }
    }
}
