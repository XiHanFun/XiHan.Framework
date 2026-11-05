// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一页文档
/// </summary>
/// <remarks>
/// 来源：文档站 <c>docs/</c>、规范 <c>framework/spec/rfcs</c> 与 <c>framework/spec/conventions</c>、各包 README
/// </remarks>
/// <param name="Path">仓库内路径，如 <c>docs/guide/caching.md</c></param>
/// <param name="Title">一级标题</param>
/// <param name="Summary">首段摘要</param>
/// <param name="Markdown">正文（去掉 frontmatter）</param>
internal sealed record DocPage(string Path, string Title, string Summary, string Markdown);
