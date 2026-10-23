// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一条规则在一个位置的结果：一次改动，或一个留给人的标记。
/// </summary>
/// <param name="Rule">规则编号，如 <c>V4-05</c>。</param>
/// <param name="File">相对路径。</param>
/// <param name="Line">1 起的行号；对整个文件的改动为 0。</param>
/// <param name="Message">标记文案；改动为空。</param>
/// <param name="IsMarker">是否标记。</param>
internal sealed record MigrationFinding(string Rule, string File, int Line, string Message, bool IsMarker);
