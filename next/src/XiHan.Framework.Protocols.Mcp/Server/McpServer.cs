// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Tools;

namespace XiHan.Framework.Protocols.Mcp.Server;

/// <summary>
/// MCP 服务端核心，与传输无关：把登记表里暴露给 MCP 的操作变成 tool 表，处理 JSON-RPC 2.0 消息（<c>initialize</c>、<c>ping</c>、<c>tools/list</c>、<c>tools/call</c>；通知不答复）。
/// <c>tools/call</c> 以新的调用上下文经完整内核管道执行操作（认证、按契约授权、事务……），操作失败按 MCP 约定以 <c>isError</c> 结果返回而不是协议错误。
/// tool 的 <c>title</c> 与 <c>description</c> 来自消息目录里的 <c>&lt;操作 ID&gt;.title</c> / <c>.description</c>（ID 里的 <c>-</c> 换成 <c>_</c>），按调用方区域翻译；没有就不给。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class McpServer
{
    /// <summary>
    /// 支持的 MCP 协议版本；<c>initialize</c> 总是答复此版本。
    /// </summary>
    public const string ProtocolVersion = "2025-06-18";

    /// <summary>
    /// Streamable HTTP 传输里声明协议版本的请求头。
    /// </summary>
    public const string ProtocolVersionHeader = "MCP-Protocol-Version";

    /// <summary>
    /// JSON-RPC 版本。
    /// </summary>
    private const string JsonRpcVersion = "2.0";

    /// <summary>
    /// 消息目录里 tool 标题键的后缀。
    /// </summary>
    private const string TitleKeySuffix = ".title";

    /// <summary>
    /// 消息目录里 tool 描述键的后缀。
    /// </summary>
    private const string DescriptionKeySuffix = ".description";

    /// <summary>
    /// 操作调用器。
    /// </summary>
    private readonly IOperationInvoker _invoker;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly McpProtocolOptions _options;

    /// <summary>
    /// 本地化器；未装配本地化时为空，tool 没有描述、失败文案按进程默认区域。
    /// </summary>
    private readonly IMessageLocalizer? _localizer;

    /// <summary>
    /// tool 名 → tool，按名字排序。
    /// </summary>
    private readonly SortedDictionary<string, McpTool> _tools;

    /// <summary>
    /// 按名字排序的 tool 列表。
    /// </summary>
    private readonly IReadOnlyList<McpTool> _toolList;

    /// <summary>
    /// 创建服务端；登记表此时必须已定型，tool 表在这里一次建成，名字不合法或重复即失败。
    /// </summary>
    /// <param name="registry">操作登记表。</param>
    /// <param name="invoker">操作调用器。</param>
    /// <param name="options">选项。</param>
    /// <param name="serverName"><c>initialize</c> 答复里的服务端名。</param>
    /// <param name="localizer">本地化器，可为空。</param>
    public McpServer(OperationRegistry registry, IOperationInvoker invoker, McpProtocolOptions options, string serverName, IMessageLocalizer? localizer)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        _invoker = invoker;
        _options = options;
        _localizer = localizer;
        ServerName = serverName;
        _tools = BuildTools(registry, options);
        _toolList = _tools.Values.ToList();
    }

    /// <summary>
    /// <c>initialize</c> 答复里的服务端名。
    /// </summary>
    public string ServerName { get; }

    /// <summary>
    /// 发布的 tool，按名字排序。
    /// </summary>
    public IReadOnlyList<McpTool> Tools => _toolList;

    /// <summary>
    /// 按名字找 tool。
    /// </summary>
    public Maybe<McpTool> FindTool(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _tools.TryGetValue(name, out var tool) ? Maybe.Some(tool) : default;
    }

    /// <summary>
    /// 协议版本不受支持时的 JSON-RPC 错误答复（<c>id</c> 为 <c>null</c>）。
    /// </summary>
    public static ReadOnlyMemory<byte> UnsupportedVersionReply(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return WriteError(default, JsonRpcErrorCodes.InvalidRequest, McpErrors.UnsupportedVersion, McpMessages.VersionUnsupported.L(("version", version), ("supported", ProtocolVersion)));
    }

    /// <summary>
    /// 处理一条 JSON-RPC 消息。通知与客户端发来的响应不答复（无值）；请求得到响应对象。
    /// </summary>
    /// <param name="message">消息的 UTF-8 JSON。</param>
    /// <param name="context">承载这条消息的调用上下文（区域、取消）。</param>
    /// <param name="createCallContext">为 <c>tools/call</c> 创建独立调用上下文的工厂：每次调用自己的作用域与管道。</param>
    public async ValueTask<Maybe<McpReply>> HandleAsync(ReadOnlyMemory<byte> message, InvocationContext context, Func<InvocationContext> createCallContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(createCallContext);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return Maybe.Some(new McpReply(WriteError(default, JsonRpcErrorCodes.ParseError, McpErrors.InvalidMessage, McpMessages.MessageNotJson.L()), IsMalformed: true));
        }

        using (document)
        {
            var root = document.RootElement;

            // 带 result / error 而没有 method 的是响应：本服务端不向客户端发请求，这样的消息只能忽略
            if (root.ValueKind == JsonValueKind.Object
                && !root.TryGetProperty("method", out _)
                && (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _)))
            {
                return default;
            }

            if (!TryReadRequest(root, out var id, out var method, out var parameters))
            {
                return Maybe.Some(new McpReply(WriteError(id, JsonRpcErrorCodes.InvalidRequest, McpErrors.InvalidMessage, McpMessages.MessageInvalid.L()), IsMalformed: true));
            }

            // 没有 id 的是通知（如 notifications/initialized），按 JSON-RPC 不答复
            if (id.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            var body = method switch
            {
                "initialize" => WriteResult(id, WriteInitialize),
                "ping" => WriteResult(id, static _ => { }),
                "tools/list" => WriteResult(id, writer => WriteTools(writer, context)),
                "tools/call" => await CallToolAsync(id, parameters, createCallContext).ConfigureAwait(false),
                _ => WriteError(id, JsonRpcErrorCodes.MethodNotFound, McpErrors.UnknownMethod, McpMessages.MethodUnknown.L(("method", method))),
            };
            return Maybe.Some(new McpReply(body, IsMalformed: false));
        }
    }

    /// <summary>
    /// 从登记表建 tool 表：排除内部稳定性与未暴露给 MCP 的操作；名字按选项覆盖或约定推导，不合法或重复视为配置错误。
    /// </summary>
    private static SortedDictionary<string, McpTool> BuildTools(OperationRegistry registry, McpProtocolOptions options)
    {
        var tools = new SortedDictionary<string, McpTool>(StringComparer.Ordinal);
        foreach (var descriptor in registry.ExposedTo(Expose.Mcp))
        {
            // 内部稳定性的操作只供进程内调用，不对外发布
            if (descriptor.Contract.Stability == Stability.Internal)
            {
                continue;
            }

            var name = options.ToolNameFor(descriptor.Contract);
            if (!McpToolName.IsValid(name))
            {
                throw new FrameworkException(McpErrors.InvalidToolName, $"Tool name '{name}' derived for operation '{descriptor.Id}' is not valid; override it with '{McpProtocolOptions.ConfigurationSection}:Tools:{descriptor.Id}'.");
            }

            if (tools.TryGetValue(name, out var existing))
            {
                throw new FrameworkException(McpErrors.DuplicateToolName, $"Tool name '{name}' is used by both operation '{existing.Descriptor.Id}' and operation '{descriptor.Id}'.");
            }

            tools.Add(name, McpTool.Create(name, descriptor));
        }

        return tools;
    }

    /// <summary>
    /// 读 JSON-RPC 请求：对象、<c>jsonrpc</c> 为 2.0、<c>method</c> 为字符串；<c>id</c> 只能是字符串或数字（缺失即通知）；<c>params</c> 只能是对象。
    /// </summary>
    private static bool TryReadRequest(JsonElement root, out JsonElement id, out string method, out JsonElement parameters)
    {
        id = default;
        method = string.Empty;
        parameters = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String || !string.Equals(version.GetString(), JsonRpcVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (!root.TryGetProperty("method", out var name) || name.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (root.TryGetProperty("id", out var requestId))
        {
            // MCP 不允许 null 的 id：无法区分通知与请求，按不合法处理
            if (requestId.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            {
                return false;
            }

            id = requestId;
        }

        if (root.TryGetProperty("params", out var requestParameters))
        {
            if (requestParameters.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            parameters = requestParameters;
        }

        method = name.GetString()!;
        return true;
    }

    /// <summary>
    /// <c>initialize</c> 结果：协议版本、能力（只有 tools，不通知变更）、服务端信息与使用说明。客户端请求的版本不同时也答复本服务端的版本，由客户端决定是否继续。
    /// </summary>
    private void WriteInitialize(Utf8JsonWriter writer)
    {
        writer.WriteString("protocolVersion", ProtocolVersion);
        writer.WriteStartObject("capabilities");
        writer.WriteStartObject("tools");
        writer.WriteBoolean("listChanged", false);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartObject("serverInfo");
        writer.WriteString("name", ServerName);
        writer.WriteString("version", _options.ServerVersion);
        writer.WriteEndObject();
        if (_options.Instructions is { Length: > 0 } instructions)
        {
            writer.WriteString("instructions", instructions);
        }
    }

    /// <summary>
    /// <c>tools/list</c> 结果：全部 tool，一页给完（不分页）。
    /// </summary>
    private void WriteTools(Utf8JsonWriter writer, InvocationContext context)
    {
        var locale = _localizer?.Options.Resolve(context);
        writer.WriteStartArray("tools");
        foreach (var tool in _toolList)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            if (locale is not null)
            {
                WriteLocalized(writer, "title", MessageKeyOf(tool.Descriptor.Id, TitleKeySuffix), locale);
                WriteLocalized(writer, "description", MessageKeyOf(tool.Descriptor.Id, DescriptionKeySuffix), locale);
            }

            writer.WritePropertyName("inputSchema");
            writer.WriteRawValue(tool.InputSchema);
            if (tool.OutputSchema is { } output)
            {
                writer.WritePropertyName("outputSchema");
                writer.WriteRawValue(output);
            }

            writer.WriteStartObject("annotations");
            writer.WriteBoolean("readOnlyHint", tool.Annotations.ReadOnly);
            writer.WriteBoolean("destructiveHint", tool.Annotations.Destructive);
            writer.WriteBoolean("idempotentHint", tool.Annotations.Idempotent);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 消息目录里有该键时写一个字符串成员。
    /// </summary>
    private void WriteLocalized(Utf8JsonWriter writer, string property, string key, string locale)
    {
        if (MessageTemplate.IsValidKey(key) && _localizer!.Find(key, locale).TryGetValue(out var text))
        {
            writer.WriteString(property, text);
        }
    }

    /// <summary>
    /// 操作 ID 对应的消息键：ID 里的连字符不合键的文法，换成下划线。
    /// </summary>
    private static string MessageKeyOf(string operationId, string suffix) => operationId.Replace('-', '_') + suffix;

    /// <summary>
    /// <c>tools/call</c>：找 tool，参数必须是对象（缺省为空对象），以新的调用上下文经完整管道执行。
    /// </summary>
    private async ValueTask<ReadOnlyMemory<byte>> CallToolAsync(JsonElement id, JsonElement parameters, Func<InvocationContext> createCallContext)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            return WriteError(id, JsonRpcErrorCodes.InvalidParams, McpErrors.InvalidMessage, McpMessages.MessageInvalid.L());
        }

        var name = nameElement.GetString()!;
        if (!_tools.TryGetValue(name, out var tool))
        {
            return WriteError(id, JsonRpcErrorCodes.InvalidParams, McpErrors.UnknownTool, McpMessages.ToolUnknown.L(("tool", name)));
        }

        ReadOnlyMemory<byte> payload = "{}"u8.ToArray();
        if (parameters.TryGetProperty("arguments", out var arguments) && arguments.ValueKind != JsonValueKind.Null)
        {
            if (arguments.ValueKind != JsonValueKind.Object)
            {
                return WriteError(id, JsonRpcErrorCodes.InvalidParams, McpErrors.InvalidArguments, McpMessages.ArgumentsInvalid.L(("tool", name)));
            }

            payload = Encoding.UTF8.GetBytes(arguments.GetRawText());
        }

        // 每次 tools/call 都是一次独立调用：新的上下文经完整管道，Authorize 阶段按契约判定，事务与作用域服务互不影响
        var call = createCallContext();
        // 来源记 MCP 与 tool 名；HTTP 传输时远端地址、User-Agent 与来源页沿用外层请求，stdio 传输没有这些
        call.SetOrigin(call.GetOrigin().TryGetValue(out var outer)
            ? outer with { Protocol = InvocationProtocols.Mcp, Target = name }
            : new InvocationOrigin { Protocol = InvocationProtocols.Mcp, Target = name });
        var response = await _invoker.InvokeAsync(call, new OperationRequest(tool.Descriptor.Id, payload)).ConfigureAwait(false);
        return WriteResult(id, writer => WriteCallResult(writer, response, call));
    }

    /// <summary>
    /// tool 调用结果：成功时文本内容是响应 JSON，响应为对象时同时给 <c>structuredContent</c>；失败时 <c>isError</c> 并以"错误码: 文案"作为文本，文案按调用方区域翻译。
    /// </summary>
    private void WriteCallResult(Utf8JsonWriter writer, OperationResponse response, InvocationContext call)
    {
        writer.WriteStartArray("content");
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        if (response.IsSuccess)
        {
            writer.WriteString("text", Encoding.UTF8.GetString(response.Payload.Span));
        }
        else
        {
            var error = response.Error!;
            var text = _localizer is not null ? _localizer.Localize(error, call) : error.Message;
            writer.WriteString("text", $"{error.Code}: {text}");
        }

        writer.WriteEndObject();
        writer.WriteEndArray();

        if (!response.IsSuccess)
        {
            writer.WriteBoolean("isError", true);
            return;
        }

        if (response.Payload.Length == 0)
        {
            return;
        }

        using var document = JsonDocument.Parse(response.Payload);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("structuredContent");
            document.RootElement.WriteTo(writer);
        }
    }

    /// <summary>
    /// 写成功响应对象：<c>result</c> 的成员由回调写出。
    /// </summary>
    private static ReadOnlyMemory<byte> WriteResult(JsonElement id, Action<Utf8JsonWriter> result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", JsonRpcVersion);
            WriteId(writer, id);
            writer.WriteStartObject("result");
            result(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// 写错误响应对象：JSON-RPC 错误码、文案与 <c>data.code</c> 里的框架错误码。
    /// </summary>
    private static ReadOnlyMemory<byte> WriteError(JsonElement id, int code, string errorCode, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", JsonRpcVersion);
            WriteId(writer, id);
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteStartObject("data");
            writer.WriteString("code", errorCode);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// 回写请求的 <c>id</c>；没有可用的 id（解析失败）时写 <c>null</c>。
    /// </summary>
    private static void WriteId(Utf8JsonWriter writer, JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteNull("id");
            return;
        }

        writer.WritePropertyName("id");
        id.WriteTo(writer);
    }
}
