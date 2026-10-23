// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 回显操作的响应：名字、调用方主体 ID 与认证方案。
/// </summary>
internal sealed record PingResponse(string Name, string Principal, string? Scheme);
