// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 登记取号操作的测试特性
/// </summary>
/// <remarks>
/// 同一实现登记为 <c>tickets.create</c> 与 <c>tickets.update</c>，执行计数是单例
/// </remarks>
internal sealed class TicketsFeature : IFeature
{
    /// <summary>
    /// 新建取号的操作 ID
    /// </summary>
    public const string Create = "tickets.create";

    /// <summary>
    /// 另一个操作 ID，用来复用同一个幂等键
    /// </summary>
    public const string Update = "tickets.update";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.tickets",
        Package = "XiHan.Framework.Caching.InMemory.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(TicketsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton<TicketCounter>();
        foreach (var id in new[] { Create, Update })
        {
            context.AddOperation<TicketsOperation, TicketRequest, TicketReply>(new OperationContract(id), TicketsJsonContext.Default.TicketRequest, TicketsJsonContext.Default.TicketReply);
        }
    }
}
