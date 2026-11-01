// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个配置节的键。
/// </summary>
/// <param name="Section">节路径。</param>
/// <param name="Options">读取它的 Options 类。</param>
/// <param name="Package">所属包。</param>
/// <param name="Binding">作为连接信息来源的绑定名。</param>
/// <param name="Keys">键。</param>
/// <param name="Appsettings">按缺省值与必填示例写出的 appsettings 片段。</param>
internal sealed record SectionView(string Section, string Options, string Package, string? Binding, IReadOnlyList<KeyView> Keys, string Appsettings);
