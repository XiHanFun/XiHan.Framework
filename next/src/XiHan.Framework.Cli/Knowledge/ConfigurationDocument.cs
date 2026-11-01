// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 配置节索引文件 <c>configuration.json</c>。
/// </summary>
/// <remarks>
/// 逐条整理自各包 Options 类的 <c>FromConfiguration</c>；测试以记录读取的配置调用它们，核对键、缺省值、必填与取值范围与代码一致。
/// </remarks>
internal sealed record ConfigurationDocument
{
    /// <summary>
    /// 全部配置节，按节路径排序。
    /// </summary>
    public IReadOnlyList<ConfigSectionEntry> Sections { get; set; } = [];
}
