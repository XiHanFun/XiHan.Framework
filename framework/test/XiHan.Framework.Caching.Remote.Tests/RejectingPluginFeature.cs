// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 只登记一个拒绝写入的 <c>cache-entries.set</c> 的测试特性
/// </summary>
/// <remarks>
/// 代替进程外插件，验证适配器原样传出插件返回的缓存域错误码
/// </remarks>
internal sealed class RejectingPluginFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.cache-plugin",
        Package = "XiHan.Framework.Caching.Remote.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(RejectingPluginFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddOperation<RejectingSetOperation, RemoteCacheOperations.SetRequest, RemoteCacheOperations.EmptyResponse>(
            new OperationContract(RemoteCacheOperations.Set) { Expose = Expose.None },
            RemoteCacheJsonContext.Default.SetRequest,
            RemoteCacheJsonContext.Default.EmptyResponse);
    }
}
