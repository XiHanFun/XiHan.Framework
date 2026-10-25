// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的错误码；登记在 <c>spec/conventions/error-codes.md</c>。这里的错误都是装配期的配置错误，随 <see cref="FrameworkException"/> 抛出，让宿主启动失败。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpErrors
{
    /// <summary>两个操作推导出同一条 HTTP 路由（方法与模板都相同）；用 <c>XiHan:Protocols:Http:Routes</c> 覆盖其中一个。</summary>
    public const string DuplicateRoute = "PROTOCOLS_CONFLICT_002";
}
