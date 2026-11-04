// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 依赖某个能力的特性
/// </summary>
/// <param name="Feature">清单 ID</param>
/// <param name="Package">所属包</param>
/// <param name="Version">要求的接口大版本下限</param>
/// <param name="Optional">是否可选</param>
internal sealed record CapabilityConsumer(string Feature, string Package, int Version, bool Optional);
