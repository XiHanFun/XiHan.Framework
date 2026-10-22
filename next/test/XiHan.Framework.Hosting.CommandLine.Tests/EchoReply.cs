// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 测试用响应：回显操作 ID、请求字段与上下文里的租户、关联 ID 与截止时间有无。
/// </summary>
/// <param name="Operation">操作 ID。</param>
/// <param name="Name">名字。</param>
/// <param name="Page">页码。</param>
/// <param name="Tags">标签。</param>
/// <param name="Tenant">Baggage 里的租户。</param>
/// <param name="Correlation">Baggage 里的关联 ID。</param>
/// <param name="HasDeadline">上下文是否带截止时间。</param>
internal sealed record EchoReply(string Operation, string? Name, int Page, string[] Tags, string? Tenant, string? Correlation, bool HasDeadline);
