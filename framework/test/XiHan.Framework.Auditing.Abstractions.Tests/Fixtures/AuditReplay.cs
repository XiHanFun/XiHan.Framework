// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 不起宿主、直接调用审计中间件的回放
/// </summary>
/// <remarks>
/// 向量给的是中间件自己的裁定：选项、上下文里的操作与安全上下文、后面的管道怎样结束。直接调用它，用例之间互不相干，也不必为每种选项起一个宿主
/// </remarks>
internal static class AuditReplay
{
    /// <summary>
    /// 以给定选项调用一次中间件
    /// </summary>
    /// <param name="options">审计选项</param>
    /// <param name="context">这次调用的上下文</param>
    /// <param name="step">中间件之后的管道</param>
    /// <returns>接收器收到的事件，以及中间件向外抛出的异常（没有抛出为 <see langword="null"/>）</returns>
    public static async Task<(IReadOnlyList<AuditEvent> Events, Exception? Thrown)> InvokeAsync(AuditingOptions options, InvocationContext context, PipelineStep step)
    {
        var sink = new RecordingAuditSink();
        var middleware = new AuditMiddleware(sink, options, TimeProvider.System, NullLoggerFactory.Instance);
        var thrown = await Record.ExceptionAsync(async () => await middleware.InvokeAsync(context, step));
        return (sink.Events, thrown);
    }
}
