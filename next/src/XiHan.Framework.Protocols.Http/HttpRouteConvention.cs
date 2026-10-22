// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// 操作 ID 到路由的默认推导（<c>spec/conventions/operation-ids.md</c>）：
/// <c>create</c> → <c>POST /{resource}</c>，<c>get</c> → <c>GET /{resource}/{id}</c>，<c>list</c> → <c>GET /{resource}</c>，
/// <c>update</c> → <c>PUT /{resource}/{id}</c>，<c>delete</c> → <c>DELETE /{resource}/{id}</c>，其他动词 → <c>POST /{resource}/{id}:{verb}</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpRouteConvention
{
    /// <summary>
    /// 按约定推导路由。
    /// </summary>
    public static HttpRoute Derive(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var resource = contract.Resource;
        return contract.Verb switch
        {
            "create" => new HttpRoute("POST", $"/{resource}"),
            "get" => new HttpRoute("GET", $"/{resource}/{{id}}"),
            "list" => new HttpRoute("GET", $"/{resource}"),
            "update" => new HttpRoute("PUT", $"/{resource}/{{id}}"),
            "delete" => new HttpRoute("DELETE", $"/{resource}/{{id}}"),
            var verb => new HttpRoute("POST", $"/{resource}/{{id}}:{verb}"),
        };
    }

    /// <summary>
    /// 成功时的状态码：<c>create</c> 为 201，其余 200。
    /// </summary>
    public static int SuccessStatusCode(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Verb == "create" ? 201 : 200;
    }
}
