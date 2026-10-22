// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// 把 HTTP 请求绑定为 XGI 载荷：一个 JSON 对象，由路由值、JSON 请求体与查询串合并而成。
/// 路由值最优先（URL 里的标识就是权威），其次请求体，最后查询串；路由值与查询串以字符串写入，多值查询串写成字符串数组。
/// 请求类型若要从路由或查询串绑定数字，序列化上下文需启用 <c>NumberHandling = AllowReadingFromString</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationRequestBinder
{
    /// <summary>
    /// 带请求体的方法；GET、DELETE 的请求体被忽略。
    /// </summary>
    private static readonly string[] _bodyMethods = ["POST", "PUT", "PATCH"];

    /// <summary>
    /// 绑定；请求体不是 JSON 对象时返回 <see cref="KernelErrors.InvalidOperationPayload"/>。
    /// </summary>
    public static async ValueTask<Result<ReadOnlyMemory<byte>>> BindAsync(HttpContext httpContext, string operationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(operationId);
        var request = httpContext.Request;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new ArrayBufferWriter<byte>();
        using var document = await ReadBodyAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is not null && document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", operationId), ("reason", "the request body must be a JSON object")));
        }

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            // 路由值最优先：URL 里的标识就是权威，不让请求体覆盖它
            foreach (var (name, value) in request.RouteValues)
            {
                if (value is not null && written.Add(name))
                {
                    writer.WriteString(name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            if (document is not null)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (written.Add(property.Name))
                    {
                        property.WriteTo(writer);
                    }
                }
            }

            // 查询串最后：多值写成数组，单值写成字符串
            foreach (var (name, values) in request.Query)
            {
                if (!written.Add(name))
                {
                    continue;
                }

                if (values.Count > 1)
                {
                    writer.WriteStartArray(name);
                    foreach (var value in values)
                    {
                        writer.WriteStringValue(value);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteString(name, values.ToString());
                }
            }

            writer.WriteEndObject();
        }

        return Result.Success(buffer.WrittenMemory);
    }

    /// <summary>
    /// 读取并解析 JSON 请求体；无请求体的方法或空请求体返回 <see langword="null"/>，非 JSON 抛出 <see cref="JsonException"/> 之外的情况由调用方翻译。
    /// </summary>
    private static async ValueTask<JsonDocument?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_bodyMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase) || request.ContentLength == 0)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        if (memory.Length == 0)
        {
            return null;
        }

        memory.Position = 0;
        try
        {
            return await JsonDocument.ParseAsync(memory, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // 非 JSON 的请求体按"不是对象"处理：交给调用方统一产出载荷不合法错误
            return JsonDocument.Parse("\"invalid\""u8.ToArray());
        }
    }
}
