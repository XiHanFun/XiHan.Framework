// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全错误码
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityErrors
{
    /// <summary>
    /// 未认证
    /// </summary>
    public const string NotAuthenticated = "SECURITY_DENIED_001";

    /// <summary>
    /// 策略拒绝
    /// </summary>
    public const string PolicyDenied = "SECURITY_DENIED_002";

    /// <summary>
    /// 凭据无效、过期或签名不符
    /// </summary>
    public const string InvalidCredential = "SECURITY_DENIED_003";

    /// <summary>
    /// 缺少权限
    /// </summary>
    public const string PermissionMissing = "SECURITY_DENIED_004";

    /// <summary>
    /// 不支持的认证方案或凭据格式不合法
    /// </summary>
    public const string UnsupportedScheme = "SECURITY_VALIDATION_001";

    /// <summary>
    /// 策略不存在或权限码未定义
    /// </summary>
    public const string PolicyNotFound = "SECURITY_NOTFOUND_001";

    /// <summary>
    /// 要撤销的权限授予不存在
    /// </summary>
    public const string GrantNotFound = "SECURITY_NOTFOUND_002";

    /// <summary>
    /// 条件无效：子句编译出错（属性未登记、运算符与值种类不搭配、条件值解析不了）
    /// </summary>
    public const string ConditionInvalid = "SECURITY_VALIDATION_002";

    /// <summary>
    /// 条件属性的值不是登记的值种类
    /// </summary>
    public const string ConditionAttributeInvalid = "SECURITY_VALIDATION_003";

    /// <summary>
    /// 按名引用的策略不存在（启动时核对）
    /// </summary>
    /// <remarks>
    /// 操作契约的 <c>Policy</c>（授权能力启动时核对，之后登记的操作在登记时返回）、端点元数据上的要求（<c>UseXiHan()</c> 建请求管道时核对）引用了未注册的策略或未登记的权限码（ADR-0080）
    /// </remarks>
    public const string UnregisteredPolicy = "SECURITY_VALIDATION_004";

    /// <summary>
    /// 授予列表的分页越界
    /// </summary>
    /// <remarks>
    /// <see cref="Permissions.PermissionGrantQuery.Skip"/> 为负或 <see cref="Permissions.PermissionGrantQuery.Take"/> 不在 1 到 <see cref="Permissions.PermissionGrantQuery.MaxTake"/> 之间：不在构造时抛出，以结果返回（HTTP 400）
    /// </remarks>
    public const string GrantQueryInvalid = "SECURITY_VALIDATION_005";

    /// <summary>
    /// 取不到条件属性的值
    /// </summary>
    public const string ConditionAttributeMissing = "SECURITY_NOTFOUND_003";

    /// <summary>
    /// 条件属性暂不可得，补齐后再求值
    /// </summary>
    public const string ConditionAttributePending = "SECURITY_UNAVAILABLE_004";

    /// <summary>
    /// 身份或授予存储不可达
    /// </summary>
    public const string StoreUnavailable = "SECURITY_UNAVAILABLE_001";

    /// <summary>
    /// 存储容量已满
    /// </summary>
    public const string CapacityExceeded = "SECURITY_UNAVAILABLE_002";

    /// <summary>
    /// 签发者的发现文档或 JWKS 不可达且没有缓存
    /// </summary>
    public const string IssuerUnavailable = "SECURITY_UNAVAILABLE_003";

    /// <summary>
    /// WebAuthn 响应核对不通过
    /// </summary>
    /// <remarks>
    /// 挑战、来源、RP ID、用户在场或验证、凭据、用户句柄或签名不符；参数 <c>reason</c>
    /// </remarks>
    public const string WebAuthnVerificationFailed = "SECURITY_DENIED_005";

    /// <summary>
    /// WebAuthn 签名计数没有增加
    /// </summary>
    /// <remarks>
    /// 认证器可能被复制；参数 <c>stored</c>、<c>received</c>
    /// </remarks>
    public const string WebAuthnCounterRegressed = "SECURITY_DENIED_006";

    /// <summary>
    /// WebAuthn 响应解析不了
    /// </summary>
    /// <remarks>
    /// base64url、JSON、CBOR、认证器数据或字段缺失、类型不对；参数 <c>reason</c>
    /// </remarks>
    public const string WebAuthnResponseMalformed = "SECURITY_VALIDATION_006";

    /// <summary>
    /// WebAuthn 公钥的算法不支持
    /// </summary>
    /// <remarks>
    /// 只认 ES256（P-256）、PS256、RS256；参数 <c>algorithm</c>
    /// </remarks>
    public const string WebAuthnAlgorithmUnsupported = "SECURITY_VALIDATION_007";
}
