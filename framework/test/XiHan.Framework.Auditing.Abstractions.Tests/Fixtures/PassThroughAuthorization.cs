// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 直接放行的 Authorize 阶段替身
/// </summary>
/// <remarks>
/// 用例只拿契约上的权限与策略给审计分类，不验授权。Authorize 阶段没有中间件时，契约要求授权的操作让装配失败（ADR-0217）
/// </remarks>
internal sealed class PassThroughAuthorization : IPipelineMiddleware
{
    /// <summary>
    /// 在 Authorize 阶段挂上本替身的特性
    /// </summary>
    public static StubFeature Feature() => new(TestManifests.Create("xihan.test.authorize"))
    {
        OnConfigure = static context => context.AddMiddleware<PassThroughAuthorization>(PipelineStage.Authorize),
    };

    /// <inheritdoc />
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
}
