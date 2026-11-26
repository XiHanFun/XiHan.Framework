// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Json;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 回显信封的操作交回的载荷
/// </summary>
/// <remarks>
/// <c>invocation.md</c> §7（ADR-0327）：JSON 对象，成员 <c>envelopes</c> 是数组，每一项是一个 JSON 对象，<c>id</c> 是信封的 <c>id</c>（字符串），<c>dataSchema</c> 是处理器拿到的 <c>dataschema</c>（字符串；没有时为 <c>null</c>、空串或不写），其余成员不作规定。与运行器的 <c>readEchoedEnvelopes</c> 同一口径
/// </remarks>
internal static class EchoedEnvelopes
{
    /// <summary>
    /// 读回显的信封
    /// </summary>
    /// <param name="payload">回显操作交回的载荷</param>
    /// <param name="problem">读不出时的原因（英文）；读得出为 <see langword="null"/></param>
    /// <returns>信封 ID 到各项 <c>dataSchema</c> 的映射，没有的记为空串；读不出为 <see langword="null"/></returns>
    public static Dictionary<string, List<string>>? Read(ReadOnlySpan<byte> payload, out string? problem)
    {
        if (JsonText.FindProblem(payload).HasValue)
        {
            problem = "the payload does not pass the JSON text checks";
            return null;
        }

        var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = JsonText.MaxDepth });
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("envelopes", out var envelopes) || envelopes.ValueKind != JsonValueKind.Array)
        {
            problem = "the payload must be a JSON object with an 'envelopes' array";
            return null;
        }

        var echoed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var index = 0;
        foreach (var envelope in envelopes.EnumerateArray())
        {
            if (envelope.ValueKind != JsonValueKind.Object || !envelope.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            {
                problem = $"item {index} of 'envelopes' must be an object with a string 'id'";
                return null;
            }

            // 不写与 null 都是没有，同空串
            var dataSchema = string.Empty;
            if (envelope.TryGetProperty("dataSchema", out var schema) && schema.ValueKind != JsonValueKind.Null)
            {
                if (schema.ValueKind != JsonValueKind.String)
                {
                    problem = $"item {index} of 'envelopes' must have a string or null 'dataSchema'";
                    return null;
                }

                dataSchema = schema.GetString()!;
            }

            var key = id.GetString()!;
            if (!echoed.TryGetValue(key, out var schemas))
            {
                schemas = [];
                echoed[key] = schemas;
            }

            schemas.Add(dataSchema);
            index++;
        }

        problem = null;
        return echoed;
    }
}
