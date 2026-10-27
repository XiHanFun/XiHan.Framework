// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeIdGenerator"/> 通过标识生成器一致性套件。
/// </summary>
public sealed class SnowflakeIdGeneratorConformanceTests : IdGeneratorConformance<long>
{
    /// <inheritdoc />
    protected override IIdGenerator<long> CreateGenerator(TimeProvider clock) =>
        new SnowflakeIdGenerator(new SnowflakeOptions(), new FixedWorkerIdSource(3), clock);
}
