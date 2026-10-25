// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 操作端点测试的响应：问候与操作里看到的操作 ID。
/// </summary>
/// <param name="Greeting">问候。</param>
/// <param name="Operation">操作里从上下文取到的操作 ID。</param>
internal sealed record PingResponse(string Greeting, string Operation);
