// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 包列表里的一行
/// </summary>
/// <param name="Id">包 ID</param>
/// <param name="Ring">环层</param>
/// <param name="Summary">一句话定位</param>
/// <param name="Features">包里特性的清单 ID</param>
internal sealed record PackageSummary(string Id, string? Ring, string? Summary, IReadOnlyList<string> Features);
