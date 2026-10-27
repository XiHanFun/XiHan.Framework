// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpProtocolCapabilities
{
    /// <summary>
    /// HTTP 协议：操作经 HTTP 暴露。
    /// </summary>
    public const string Http = "protocols.http";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
