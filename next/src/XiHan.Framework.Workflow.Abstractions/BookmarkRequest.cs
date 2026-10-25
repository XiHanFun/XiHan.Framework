// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 活动挂起时请求的书签。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BookmarkRequest
{
    /// <summary>
    /// 种类（<see cref="BookmarkKinds"/>）。
    /// </summary>
    public required string Kind
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The bookmark kind must not be empty.", nameof(value));
    }

    /// <summary>
    /// 键。
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// 载荷。
    /// </summary>
    public JsonObject? Payload { get; init; }

    /// <summary>
    /// 到期时间。
    /// </summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }
}
