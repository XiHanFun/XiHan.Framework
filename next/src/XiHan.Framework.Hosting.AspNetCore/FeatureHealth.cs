// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 探针端点中的单个特性状态。
/// </summary>
/// <param name="Id">清单 ID。</param>
/// <param name="State">生命周期状态。</param>
/// <param name="Error">失败原因的错误码。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record FeatureHealth(string Id, string State, string? Error);
