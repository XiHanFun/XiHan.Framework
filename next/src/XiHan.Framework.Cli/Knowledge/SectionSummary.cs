// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 配置节列表里的一行。
/// </summary>
/// <param name="Section">节路径。</param>
/// <param name="Package">所属包。</param>
/// <param name="Options">读取它的 Options 类。</param>
/// <param name="Keys">键的个数。</param>
internal sealed record SectionSummary(string Section, string Package, string Options, int Keys);
