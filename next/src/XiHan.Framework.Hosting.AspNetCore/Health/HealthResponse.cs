// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Health;

/// <summary>
/// 探针端点的响应体。
/// </summary>
/// <param name="Status"><c>pass</c> 或 <c>fail</c>。</param>
/// <param name="State">应用聚合状态。</param>
/// <param name="Features">各特性状态。</param>
/// <param name="Contributions">各健康贡献条目（如插件）的状态。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HealthResponse(string Status, string State, IReadOnlyList<FeatureHealth> Features, IReadOnlyList<FeatureHealth> Contributions);
