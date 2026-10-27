// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="GuidV7IdGenerator"/> 通过标识生成器一致性套件。
/// </summary>
public sealed class GuidV7IdGeneratorConformanceTests : IdGeneratorConformance<Guid>
{
    /// <inheritdoc />
    protected override IIdGenerator<Guid> CreateGenerator(TimeProvider clock) => new GuidV7IdGenerator(clock);
}
