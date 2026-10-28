// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "security";

    /// <summary>
    /// 调用需要认证。
    /// </summary>
    public const string AuthenticationRequired = "security.authentication.required";

    /// <summary>
    /// 认证方案不受支持。参数：<c>scheme</c>。
    /// </summary>
    public const string UnsupportedScheme = "security.authentication.unsupported_scheme";

    /// <summary>
    /// 策略不存在。参数：<c>policy</c>。
    /// </summary>
    public const string PolicyNotFound = "security.policy.not_found";

    /// <summary>
    /// 缺少权限。参数：<c>permission</c>。
    /// </summary>
    public const string PermissionMissing = "security.permission.missing";

    /// <summary>
    /// 要撤销的权限授予不存在。参数：<c>subject</c>、<c>permission</c>、<c>tenant</c>（全局为 <c>*</c>）。
    /// </summary>
    public const string GrantNotFound = "security.grant.not_found";

    /// <summary>
    /// API Key 无效。
    /// </summary>
    public const string InvalidApiKey = "security.api_key.invalid";

    /// <summary>
    /// 授予存储容量已满。参数：<c>capacity</c>。
    /// </summary>
    public const string GrantStoreCapacityExceeded = "security.grant_store.capacity_exceeded";

    /// <summary>
    /// 主体存储容量已满。参数：<c>capacity</c>。
    /// </summary>
    public const string PrincipalStoreCapacityExceeded = "security.principal_store.capacity_exceeded";

    /// <summary>
    /// 令牌无效、过期或签名不符。
    /// </summary>
    public const string InvalidToken = "security.token.invalid";

    /// <summary>
    /// 令牌缺少主体声明。参数：<c>claim</c>。
    /// </summary>
    public const string TokenSubjectMissing = "security.token.subject_missing";

    /// <summary>
    /// 签发者的元数据不可达。参数：<c>authority</c>。
    /// </summary>
    public const string IssuerUnavailable = "security.issuer.unavailable";

    /// <summary>
    /// 条件运算符不受支持。参数：<c>operator</c>（整数值）。
    /// </summary>
    public const string ConditionOperatorUnsupported = "security.condition.operator_unsupported";

    /// <summary>
    /// 条件属性名为空。
    /// </summary>
    public const string ConditionAttributeRequired = "security.condition.attribute_required";

    /// <summary>
    /// 条件属性未登记。参数：<c>attribute</c>。
    /// </summary>
    public const string ConditionAttributeUnknown = "security.condition.attribute_unknown";

    /// <summary>
    /// 运算符不能用于该值种类的属性。参数：<c>operator</c>、<c>kind</c>、<c>attribute</c>。
    /// </summary>
    public const string ConditionOperatorMismatch = "security.condition.operator_mismatch";

    /// <summary>
    /// 运算符不能用于该值种类的多值属性。参数：<c>operator</c>、<c>kind</c>、<c>attribute</c>。
    /// </summary>
    public const string ConditionOperatorMismatchMultiValued = "security.condition.operator_mismatch_multi_valued";

    /// <summary>
    /// 条件值为空。
    /// </summary>
    public const string ConditionValueRequired = "security.condition.value_required";

    /// <summary>
    /// 条件值不是合法的值种类。参数：<c>kind</c>、<c>value</c>。
    /// </summary>
    public const string ConditionValueInvalid = "security.condition.value_invalid";

    /// <summary>
    /// 集合为空。
    /// </summary>
    public const string ConditionListEmpty = "security.condition.list_empty";

    /// <summary>
    /// 集合元素不是合法的值种类。参数：<c>kind</c>、<c>element</c>。
    /// </summary>
    public const string ConditionListElementInvalid = "security.condition.list_element_invalid";

    /// <summary>
    /// JSON 数组里的集合元素不是字符串、数字或布尔。参数：<c>element</c>。
    /// </summary>
    public const string ConditionListElementUnsupported = "security.condition.list_element_unsupported";

    /// <summary>
    /// 集合不是合法的 JSON 数组。参数：<c>reason</c>。
    /// </summary>
    public const string ConditionListJsonInvalid = "security.condition.list_json_invalid";

    /// <summary>
    /// 区间对象缺 <c>min</c> 或 <c>max</c>。
    /// </summary>
    public const string ConditionRangeObjectInvalid = "security.condition.range_object_invalid";

    /// <summary>
    /// 区间不是正好两个值。
    /// </summary>
    public const string ConditionRangeArity = "security.condition.range_arity";

    /// <summary>
    /// 区间的值不是合法的值种类。参数：<c>kind</c>、<c>value</c>。
    /// </summary>
    public const string ConditionRangeValueInvalid = "security.condition.range_value_invalid";

    /// <summary>
    /// 区间下限大于上限。参数：<c>value</c>。
    /// </summary>
    public const string ConditionRangeReversed = "security.condition.range_reversed";

    /// <summary>
    /// 取不到条件属性的值。参数：<c>attribute</c>。
    /// </summary>
    public const string ConditionAttributeMissing = "security.condition.attribute_missing";

    /// <summary>
    /// 条件属性暂不可得。参数：<c>attribute</c>。
    /// </summary>
    public const string ConditionAttributePending = "security.condition.attribute_pending";

    /// <summary>
    /// 条件属性的值不是登记的值种类。参数：<c>attribute</c>、<c>kind</c>、<c>value</c>。
    /// </summary>
    public const string ConditionAttributeInvalid = "security.condition.attribute_invalid";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(SecurityMessages)));

    /// <summary>
    /// 目录实例；首次访问时从嵌入资源装载。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
