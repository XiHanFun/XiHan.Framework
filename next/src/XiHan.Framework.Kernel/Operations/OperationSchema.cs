// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作的请求与响应 JSON Schema（draft 2020-12，UTF-16 文本）。
/// </summary>
/// <remarks>
/// 由源生成器从类型产出。协议适配器据此发布 OpenAPI 与 MCP tool 描述。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationSchema
{
    /// <summary>
    /// 创建 Schema。
    /// </summary>
    /// <remarks>
    /// 两段都必须是 JSON 对象。
    /// </remarks>
    /// <param name="request">请求类型的 Schema。</param>
    /// <param name="response">响应类型的 Schema。</param>
    public OperationSchema(string request, string response)
    {
        Request = Validate(request, nameof(request));
        Response = Validate(response, nameof(response));
    }

    /// <summary>
    /// 请求类型的 Schema。
    /// </summary>
    public string Request { get; }

    /// <summary>
    /// 响应类型的 Schema。
    /// </summary>
    public string Response { get; }

    /// <summary>
    /// 校验文本是 JSON 对象。
    /// </summary>
    private static string Validate(string schema, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema, paramName);
        try
        {
            using var document = JsonDocument.Parse(schema);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The schema must be a JSON object.", paramName);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"The schema is not valid JSON: {exception.Message}", paramName, exception);
        }

        return schema;
    }
}
