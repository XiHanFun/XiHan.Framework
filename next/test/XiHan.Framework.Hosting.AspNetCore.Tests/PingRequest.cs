// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 操作端点测试的请求。
/// </summary>
/// <param name="Name">名字。</param>
internal sealed record PingRequest(string? Name);
