// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 提供某个能力的特性。
/// </summary>
/// <param name="Feature">清单 ID。</param>
/// <param name="Type">特性类型。</param>
/// <param name="Package">所属包。</param>
/// <param name="Role">角色：<c>default</c> 在没有显式 Provider 时被选中。</param>
/// <param name="Version">提供的接口大版本。</param>
internal sealed record CapabilityProvider(string Feature, string Type, string Package, string Role, int Version);
