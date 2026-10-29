// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，特性装配时注册 <see cref="Catalog"/>；目前都是 OpenAPI 文档里由生成器统一写的文案（ADR-0052）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class HttpMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "protocols.http";

    /// <summary>
    /// 操作说明末尾的权限要求。
    /// </summary>
    /// <remarks>
    /// 参数：<c>permission</c>（权限码）、<c>name</c>（权限的显示名）。
    /// </remarks>
    public const string OpenApiPermissionRequired = "protocols.http.openapi_permission_required";

    /// <summary>
    /// 操作说明末尾的权限要求（权限没有显示名）。
    /// </summary>
    /// <remarks>
    /// 参数：<c>permission</c>。
    /// </remarks>
    public const string OpenApiPermissionCodeRequired = "protocols.http.openapi_permission_code_required";

    /// <summary>
    /// 操作说明末尾的策略要求。
    /// </summary>
    /// <remarks>
    /// 参数：<c>policy</c>。
    /// </remarks>
    public const string OpenApiPolicyRequired = "protocols.http.openapi_policy_required";

    /// <summary>
    /// 操作说明末尾的登录要求。
    /// </summary>
    public const string OpenApiAuthenticationRequired = "protocols.http.openapi_authentication_required";

    /// <summary>
    /// 成功响应的说明。
    /// </summary>
    public const string OpenApiSuccess = "protocols.http.openapi_success";

    /// <summary>
    /// 失败响应的说明。
    /// </summary>
    public const string OpenApiError = "protocols.http.openapi_error";

    /// <summary>
    /// <c>x-tagGroups</c> 里没进任何组的标签所在组的名字。
    /// </summary>
    public const string OpenApiUngrouped = "protocols.http.openapi_ungrouped";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(HttpMessages));
}
