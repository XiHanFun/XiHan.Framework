// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 一块网卡的累计收发字节数。
/// </summary>
/// <param name="Id">网卡 ID。</param>
/// <param name="Received">累计接收字节数；读不到为空。</param>
/// <param name="Sent">累计发送字节数；读不到为空。</param>
internal readonly record struct NetworkCounters(string Id, long? Received, long? Sent);
