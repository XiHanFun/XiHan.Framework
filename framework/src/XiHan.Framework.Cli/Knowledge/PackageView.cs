// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个包的详情
/// </summary>
/// <param name="Id">包 ID</param>
/// <param name="Ring">环层</param>
/// <param name="Summary">一句话定位</param>
/// <param name="Description">包说明</param>
/// <param name="Install">安装命令</param>
/// <param name="References">引用的框架包</param>
/// <param name="Dependencies">引用的第三方包</param>
/// <param name="FrameworkReferences">引用的共享框架</param>
/// <param name="Features">包里的特性及其清单</param>
/// <param name="Sections">包的 Options 类读取的配置节</param>
/// <param name="Docs">相关文档页的路径</param>
internal sealed record PackageView(
    string Id,
    string? Ring,
    string? Summary,
    string Description,
    string Install,
    IReadOnlyList<string> References,
    IReadOnlyList<PackageDependency> Dependencies,
    IReadOnlyList<string> FrameworkReferences,
    IReadOnlyList<FeatureEntry> Features,
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> Docs);
