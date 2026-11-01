// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_config</c> 的输出。
/// </summary>
/// <param name="Version">索引对应的框架版本。</param>
/// <param name="Sections">命中的配置节及其键。</param>
/// <param name="Index">没有给查询时的全部配置节。</param>
internal sealed record ConfigResponse(string Version, IReadOnlyList<SectionView>? Sections, IReadOnlyList<SectionSummary>? Index);
