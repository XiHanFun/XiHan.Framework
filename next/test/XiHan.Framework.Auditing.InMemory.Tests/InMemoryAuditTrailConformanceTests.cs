// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Conformance;
using XiHan.Framework.Kernel.Diagnostics;

namespace XiHan.Framework.Auditing.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryAuditTrail"/> 跑完整套 <see cref="AuditTrailConformance"/> 用例。
/// </summary>
public sealed class InMemoryAuditTrailConformanceTests : AuditTrailConformance
{
    /// <summary>
    /// 内存夹具：接收器与查询是同一个对象。
    /// </summary>
    private sealed class Fixture : AuditTrailFixture
    {
        /// <summary>
        /// 被测对象。
        /// </summary>
        private readonly InMemoryAuditTrail _trail = new(new InMemoryAuditingOptions(), NullLoggerFactory.Instance);

        /// <inheritdoc />
        public override IAuditSink Sink => _trail;

        /// <inheritdoc />
        public override IAuditTrail Trail => _trail;
    }

    /// <inheritdoc />
    protected override Task<AuditTrailFixture> CreateFixtureAsync() => Task.FromResult<AuditTrailFixture>(new Fixture());
}
