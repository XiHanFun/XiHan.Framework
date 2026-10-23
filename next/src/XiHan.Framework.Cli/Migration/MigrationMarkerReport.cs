// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一个留给人的标记。
/// </summary>
/// <param name="Rule">规则编号。</param>
/// <param name="File">相对路径。</param>
/// <param name="Line">原文件里的 1 起行号。</param>
/// <param name="Message">标记文案。</param>
internal sealed record MigrationMarkerReport(string Rule, string File, int Line, string Message);
