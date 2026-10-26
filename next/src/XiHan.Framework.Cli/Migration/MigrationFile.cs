// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 正在迁移的一个文件：绝对路径、相对根目录的显示路径、所属项目目录与换行符。
/// </summary>
/// <param name="Path">绝对路径。</param>
/// <param name="RelativePath">相对 <c>--path</c> 的路径，报告里用。</param>
/// <param name="ProjectDirectory">最近的含 <c>.csproj</c> 的祖先目录；没有为 <see langword="null"/>。</param>
/// <param name="NewLine">文件使用的换行符；生成的标记与成员沿用。</param>
internal sealed record MigrationFile(string Path, string RelativePath, string? ProjectDirectory, string NewLine);
