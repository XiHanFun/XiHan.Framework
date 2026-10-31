// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 取号操作的响应。
/// </summary>
/// <param name="Number">号码：操作每执行一次加一，回放不会变。</param>
/// <param name="Name">取号人。</param>
internal sealed record TicketReply(int Number, string Name);
