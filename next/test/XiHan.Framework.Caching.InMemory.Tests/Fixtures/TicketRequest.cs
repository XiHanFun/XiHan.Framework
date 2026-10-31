// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 取号操作的请求。
/// </summary>
/// <param name="Name">取号人；为 <c>fail</c> 时操作返回冲突。</param>
internal sealed record TicketRequest(string Name);
