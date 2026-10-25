// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 登记四个回显操作：<c>echoes.create</c>（AI + HTTP，带 Schema）、<c>echoes.rename</c>（AI，无 Schema）、<c>echoes.secret</c>（只 HTTP）、<c>echoes.audit</c>（Internal）。
/// </summary>
internal sealed class EchoFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.echoes",
        Package = "XiHan.Framework.AI.Abstractions.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(EchoFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        Add(context, new OperationContract("echoes.create", expose: Expose.Ai | Expose.Http), new OperationSchema(EchoOperation.RequestSchema, EchoOperation.ResponseSchema));
        Add(context, new OperationContract("echoes.rename", expose: Expose.Ai));
        Add(context, new OperationContract("echoes.secret", expose: Expose.Http));
        Add(context, new OperationContract("echoes.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 登记一个回显操作。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract, OperationSchema? schema = null)
        => context.AddOperation<EchoOperation, EchoRequest, EchoResponse>(contract, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse, schema);
}
