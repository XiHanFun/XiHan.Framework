// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 一个生成的文件：相对输出目录的路径（正斜杠）与内容。
/// </summary>
/// <param name="Path">相对路径。</param>
/// <param name="Content">内容，LF 换行。</param>
internal sealed record PackOutput(string Path, string Content);
