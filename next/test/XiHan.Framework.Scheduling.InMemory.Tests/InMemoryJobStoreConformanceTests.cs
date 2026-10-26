// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Conformance;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 内存作业存储通过存储套件。
/// </summary>
public sealed class InMemoryJobStoreConformanceTests : JobStoreConformance
{
    /// <inheritdoc />
    protected override Task<(IJobStore Store, IAsyncDisposable Host)> CreateAsync()
        => Task.FromResult<(IJobStore, IAsyncDisposable)>((new InMemoryJobStore(new InMemorySchedulingOptions()), new NoopDisposable()));

    /// <summary>
    /// 内存存储没有要释放的资源。
    /// </summary>
    private sealed class NoopDisposable : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
