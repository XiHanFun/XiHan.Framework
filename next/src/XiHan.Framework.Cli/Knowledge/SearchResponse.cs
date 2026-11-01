// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_search</c> 的输出。
/// </summary>
/// <param name="Version">索引对应的框架版本。</param>
/// <param name="Results">检索结果，按相关度排序。</param>
/// <param name="Page">要读的那一页全文。</param>
internal sealed record SearchResponse(string Version, IReadOnlyList<SearchHit>? Results, DocPage? Page);
