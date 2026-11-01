// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个能力的提供者与依赖方。
/// </summary>
/// <param name="Id">能力 ID。</param>
/// <param name="Providers">声明提供它的特性。</param>
/// <param name="RequiredBy">声明依赖它的特性。</param>
internal sealed record CapabilityView(string Id, IReadOnlyList<CapabilityProvider> Providers, IReadOnlyList<CapabilityConsumer> RequiredBy);
