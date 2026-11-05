// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_info</c> 的输出
/// </summary>
/// <param name="Version">索引对应的框架版本</param>
/// <param name="Packages">命中的包的详情</param>
/// <param name="Index">没有给名字时的全部包</param>
/// <param name="Capability">名字是能力 ID 时的提供者与依赖方</param>
internal sealed record InfoResponse(string Version, IReadOnlyList<PackageView>? Packages, IReadOnlyList<PackageSummary>? Index, CapabilityView? Capability);
