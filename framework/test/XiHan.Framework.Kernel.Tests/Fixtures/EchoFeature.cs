// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 登记回声操作并在 Authorize 阶段挂上捕获中间件的测试特性
/// </summary>
/// <remarks>
/// 可改操作 ID 以制造重复登记
/// </remarks>
/// <param name="manifestId">清单 ID</param>
/// <param name="operationId">登记的操作 ID</param>
/// <param name="capture">是否在 Authorize 阶段挂捕获中间件；同一应用里只能有一个实例挂它</param>
/// <param name="schema">登记时附带的请求 / 响应 Schema；为空时不校验 Schema</param>
/// <param name="registerPermission">是否登记契约要求的权限码 <c>echoes.write</c>；为假时由别的特性登记，或用来验证未登记即装配失败</param>
internal sealed class EchoFeature(string manifestId = "xihan.test.echo", string operationId = EchoOperation.Id, bool capture = true, OperationSchema? schema = null, bool registerPermission = true) : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = manifestId,
        Package = "XiHan.Framework.Kernel.Tests",
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
        if (capture)
        {
            context.Services.TryAddSingleton<OperationCapture>();
            context.AddMiddleware<OperationCaptureMiddleware>(PipelineStage.Authorize);
        }

        // 契约引用的权限码必须已登记（装配完成时核对）；同一应用里可装多个实例，只登记一次
        var permission = EchoOperation.Contract.Permission!;
        if (registerPermission && !context.Permissions.Find(permission).HasValue)
        {
            context.AddPermission(new PermissionDefinition(permission, "Write echoes"));
        }

        context.AddOperation<EchoOperation, EchoRequest, EchoResponse>(
            new(operationId) { Stability = Stability.Stable, Expose = EchoOperation.Contract.Expose, Permission = EchoOperation.Contract.Permission },
            EchoJsonContext.Default.EchoRequest,
            EchoJsonContext.Default.EchoResponse,
            schema);
    }
}
