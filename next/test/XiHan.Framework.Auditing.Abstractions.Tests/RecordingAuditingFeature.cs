// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 以记录接收器提供 <c>auditing.trail@1</c> 的测试 Provider，按 Provider 的方式调用 <see cref="AuditingFeatureContextExtensions.AddAuditing"/>。
/// </summary>
internal sealed class RecordingAuditingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = TestManifests.Create("xihan.auditing.recording", provides: ["auditing.trail@1"], requires: ["kernel.hosting@1"]);

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<RecordingAuditSink>();
        context.Services.AddSingleton<IAuditSink>(static sp => sp.GetRequiredService<RecordingAuditSink>());
        context.AddAuditing();
    }
}
