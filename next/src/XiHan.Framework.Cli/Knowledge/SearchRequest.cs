// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_search</c> 的输入
/// </summary>
/// <remarks>
/// 给 <see cref="Query"/> 检索，给 <see cref="Page"/> 取一页全文，至少给一个
/// </remarks>
internal sealed record SearchRequest
{
    /// <summary>
    /// 检索词，空格分隔的词须同时出现
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// 要读全文的文档页
    /// </summary>
    /// <remarks>
    /// 仓库内路径（可省略 <c>.md</c> 与 <c>docs-next/</c>）、RFC 文件名或包 ID（取它的 README）
    /// </remarks>
    public string? Page { get; set; }

    /// <summary>
    /// 最多返回的结果数
    /// </summary>
    /// <remarks>
    /// 1 ~ 50，缺省 10
    /// </remarks>
    public int? Limit { get; set; }
}
