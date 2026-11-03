// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Persistence.InMemory;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 发件箱一致性套件跑在内存持久化上。
/// </summary>
/// <remarks>
/// 与数据库 Provider（SqlSugar 的 SQLite 与 PostgreSQL）跑同一套用例，内存实现与数据库实现的投递、重试与死信行为一致。
/// </remarks>
public sealed class InMemoryOutboxConformanceTests : OutboxConformance
{
    /// <inheritdoc />
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
