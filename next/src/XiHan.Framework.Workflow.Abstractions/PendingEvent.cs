// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 等波次结束后发布的事件。
/// </summary>
/// <param name="Topic">主题。</param>
/// <param name="Data">数据。</param>
/// <param name="Subject">信封主题（自定义事件的事件名）。</param>
internal sealed record PendingEvent(string Topic, WorkflowEventData Data, string? Subject = null);
