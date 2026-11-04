// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 知识库三个操作的请求与响应 Schema
/// </summary>
/// <remarks>
/// MCP 的 <c>inputSchema</c> 与 <c>outputSchema</c> 取自这里（<c>limit</c> 的上限 50 即 <see cref="KnowledgeQueries.MaxLimit"/>）；内核的 Validate 阶段按请求 Schema 校验参数（如 <c>limit</c> 的范围）。参数的说明写在消息目录的 tool 描述里，Schema 不带文案
/// </remarks>
internal static class KnowledgeSchemas
{
    /// <summary>
    /// <c>cli.info</c>
    /// </summary>
    public static OperationSchema Info { get; } = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"InfoRequest","type":"object","properties":{"name":{"type":"string"}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"InfoResponse","type":"object","properties":{"version":{"type":"string"},"packages":{"type":"array","items":{"type":"object"}},"index":{"type":"array","items":{"type":"object"}},"capability":{"type":"object"}},"required":["version"]}""");

    /// <summary>
    /// <c>cli.config</c>
    /// </summary>
    public static OperationSchema Config { get; } = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"ConfigRequest","type":"object","properties":{"query":{"type":"string"}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"ConfigResponse","type":"object","properties":{"version":{"type":"string"},"sections":{"type":"array","items":{"type":"object"}},"index":{"type":"array","items":{"type":"object"}}},"required":["version"]}""");

    /// <summary>
    /// <c>cli.search</c>
    /// </summary>
    public static OperationSchema Search { get; } = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"SearchRequest","type":"object","properties":{"query":{"type":"string"},"page":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":50}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"SearchResponse","type":"object","properties":{"version":{"type":"string"},"results":{"type":"array","items":{"type":"object"}},"page":{"type":"object"}},"required":["version"]}""");
}
