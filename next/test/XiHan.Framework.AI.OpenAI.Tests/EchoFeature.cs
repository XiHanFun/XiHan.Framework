// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// 一个暴露给 AI 的回显操作 <c>echoes.create</c>。
/// </summary>
internal sealed class EchoFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.echoes",
        Package = "XiHan.Framework.AI.OpenAI.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(EchoFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<EchoOperation, EchoRequest, EchoResponse>(
            new OperationContract("echoes.create", expose: Expose.Ai),
            EchoJsonContext.Default.EchoRequest,
            EchoJsonContext.Default.EchoResponse,
            new OperationSchema("""{"type":"object","properties":{"text":{"type":"string"},"repeat":{"type":"integer"}},"required":["text"]}""", """{"type":"object","properties":{"echo":{"type":"string"}}}"""));
}
