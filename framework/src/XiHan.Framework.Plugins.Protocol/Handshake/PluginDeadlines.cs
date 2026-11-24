// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 插件协议一侧生效的截止时间
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §2（ADR-0237）：<c>InvocationMetadata.deadline</c> 的文本由原语 <see cref="DeadlineText"/> 读写，与 HTTP 载体的 <c>xihan-deadline</c> 同一条文法（RFC-0003 规则 8，ADR-0276、ADR-0278）；读不出就当作没有，生效的截止时间取它与 gRPC 调用自身截止时间中较早的一个
/// </remarks>
internal static class PluginDeadlines
{
    /// <summary>
    /// 生效的截止时间
    /// </summary>
    /// <remarks>
    /// 元数据里的截止时间（读不出当作没有）与 gRPC 调用自身截止时间中较早的一个；两者都没有为 <see langword="null"/>
    /// </remarks>
    /// <param name="metadata">元数据的 <c>deadline</c></param>
    /// <param name="call">gRPC 调用自身的截止时间；没有为 <see langword="null"/></param>
    public static DateTimeOffset? Effective(string? metadata, DateTimeOffset? call)
        => Effective(metadata, call, out _);

    /// <summary>
    /// 生效的截止时间，并交出元数据里的截止时间是不是写了却读不出
    /// </summary>
    /// <remarks>
    /// 取法同 <see cref="Effective(string?, DateTimeOffset?)"/>。<paramref name="unreadable"/> 供宿主记限频的告警（ADR-0279）：空串是协议里「没有给」的写法，不算读不出
    /// </remarks>
    /// <param name="metadata">元数据的 <c>deadline</c></param>
    /// <param name="call">gRPC 调用自身的截止时间；没有为 <see langword="null"/></param>
    /// <param name="unreadable">元数据的 <c>deadline</c> 不为空而读不出</param>
    public static DateTimeOffset? Effective(string? metadata, DateTimeOffset? call, out bool unreadable)
    {
        DateTimeOffset? declared = DeadlineText.TryParse(metadata, out var parsed) ? parsed : null;
        unreadable = declared is null && !string.IsNullOrEmpty(metadata);
        return (declared, call) switch
        {
            ({ } a, { } b) => a <= b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
    }

    /// <summary>
    /// gRPC 服务端看到的调用截止时间
    /// </summary>
    /// <remarks>
    /// <c>ServerCallContext.Deadline</c> 以 <see cref="DateTime.MaxValue"/> 表示没有截止时间，其余是 UTC
    /// </remarks>
    public static DateTimeOffset? FromCall(DateTime deadline)
        => deadline == DateTime.MaxValue ? null : new DateTimeOffset(DateTime.SpecifyKind(deadline, DateTimeKind.Utc));
}
