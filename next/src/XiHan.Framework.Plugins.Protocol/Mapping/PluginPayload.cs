// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 代理操作的请求与响应类型：原样搬运的 UTF-8 JSON，不在宿主侧解码。
/// </summary>
/// <param name="Bytes">JSON 字节。</param>
internal sealed record PluginPayload(ReadOnlyMemory<byte> Bytes);
