// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一条检索结果
/// </summary>
/// <param name="Path">文档页路径</param>
/// <param name="Title">标题</param>
/// <param name="Summary">首段摘要</param>
/// <param name="Score">相关度</param>
/// <param name="Snippet">第一处命中的摘录</param>
internal sealed record SearchHit(string Path, string Title, string Summary, int Score, string Snippet);
