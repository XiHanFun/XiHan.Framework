// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 当前项目的位置与引用的框架版本。
/// </summary>
/// <param name="Root">项目根目录：所在 git 仓库的根，不在仓库里时为向上第一个含解决方案或项目文件的目录；都没有时为空。</param>
/// <param name="FrameworkVersion">项目里 <c>XiHan.Framework.*</c> 包引用最常见的版本；没有引用时为空。</param>
internal sealed record ProjectInfo(string? Root, string? FrameworkVersion);
