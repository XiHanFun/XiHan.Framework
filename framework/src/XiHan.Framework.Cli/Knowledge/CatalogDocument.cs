// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 包与特性清单的索引文件 <c>catalog.json</c>
/// </summary>
/// <remarks>
/// 由测试从工作区的项目文件与各特性的 <c>Manifest</c> 生成（<c>framework/scripts/Update-CliKnowledge.ps1</c>），与代码不一致时测试失败
/// </remarks>
internal sealed record CatalogDocument
{
    /// <summary>
    /// 全部正式包，按包 ID 排序
    /// </summary>
    public IReadOnlyList<PackageEntry> Packages { get; set; } = [];

    /// <summary>
    /// 全部公开特性的清单，按类型名排序
    /// </summary>
    public IReadOnlyList<FeatureEntry> Features { get; set; } = [];
}
