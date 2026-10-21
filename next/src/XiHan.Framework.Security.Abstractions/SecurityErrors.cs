// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityErrors
{
    /// <summary>未认证。</summary>
    public const string NotAuthenticated = "SECURITY_DENIED_001";

    /// <summary>策略拒绝。</summary>
    public const string PolicyDenied = "SECURITY_DENIED_002";

    /// <summary>凭据无效、过期或签名不符。</summary>
    public const string InvalidCredential = "SECURITY_DENIED_003";

    /// <summary>缺少权限。</summary>
    public const string PermissionMissing = "SECURITY_DENIED_004";

    /// <summary>不支持的认证方案或凭据格式不合法。</summary>
    public const string UnsupportedScheme = "SECURITY_VALIDATION_001";

    /// <summary>策略不存在或权限码未定义。</summary>
    public const string PolicyNotFound = "SECURITY_NOTFOUND_001";

    /// <summary>身份或授予存储不可达。</summary>
    public const string StoreUnavailable = "SECURITY_UNAVAILABLE_001";

    /// <summary>存储容量已满。</summary>
    public const string CapacityExceeded = "SECURITY_UNAVAILABLE_002";
}
