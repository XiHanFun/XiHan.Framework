// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel;

/// <summary>
/// 内核失败结果的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，由本地化默认实现装配。
/// 启动期的 <see cref="FrameworkException"/>（配置与编程错误）不走消息键。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class KernelMessages
{
    /// <summary>清单不是合法 JSON。参数：<c>reason</c>。</summary>
    public const string ManifestInvalidJson = "kernel.manifest.invalid_json";

    /// <summary>清单为空。</summary>
    public const string ManifestEmpty = "kernel.manifest.empty";

    /// <summary>清单文件不存在。参数：<c>path</c>。</summary>
    public const string ManifestFileMissing = "kernel.manifest.file_missing";

    /// <summary>清单内容不合法。参数：<c>reason</c>。</summary>
    public const string ManifestInvalid = "kernel.manifest.invalid";

    /// <summary>调用期间发生未处理异常。</summary>
    public const string UnhandledException = "kernel.invocation.unhandled_exception";

    /// <summary>特性启动失败。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string FeatureStartFailed = "kernel.feature.start_failed";

    /// <summary>特性停止超时。参数：<c>id</c>。</summary>
    public const string FeatureStopTimeout = "kernel.feature.stop_timeout";

    /// <summary>特性停止失败。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string FeatureStopFailed = "kernel.feature.stop_failed";

    /// <summary>调用开始前已超过截止时间。</summary>
    public const string DeadlineBeforeStart = "kernel.invocation.deadline_before_start";

    /// <summary>调用超过截止时间。</summary>
    public const string DeadlineExceeded = "kernel.invocation.deadline_exceeded";

    /// <summary>调用被取消。</summary>
    public const string Cancelled = "kernel.invocation.cancelled";

    /// <summary>策略拒绝。</summary>
    public const string DeniedByPolicy = "kernel.authorization.denied_by_policy";

    /// <summary>权限注册表已满。参数：<c>capacity</c>。</summary>
    public const string PermissionRegistryFull = "kernel.permission.registry_full";

    /// <summary>权限码重复登记。参数：<c>code</c>。</summary>
    public const string DuplicatePermission = "kernel.permission.duplicate";

    /// <summary>应用未处于服务状态。参数：<c>state</c>。</summary>
    public const string NotServing = "kernel.hosting.not_serving";

    /// <summary>操作未登记。参数：<c>id</c>。</summary>
    public const string UnknownOperation = "kernel.operation.unknown";

    /// <summary>操作载荷不合法。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string InvalidOperationPayload = "kernel.operation.invalid_payload";

    /// <summary>类型化调用的类型与登记不符。参数：<c>id</c>、<c>expected</c>、<c>actual</c>。</summary>
    public const string OperationTypeMismatch = "kernel.operation.type_mismatch";

    /// <summary>操作 ID 重复登记。参数：<c>id</c>、<c>owner</c>。</summary>
    public const string DuplicateOperation = "kernel.operation.duplicate";

    /// <summary>操作登记表已满。参数：<c>capacity</c>。</summary>
    public const string OperationRegistryFull = "kernel.operation.registry_full";

    /// <summary>操作成功结束却没有产生响应对象。参数：<c>id</c>。</summary>
    public const string OperationNoResponse = "kernel.operation.no_response";

    /// <summary>请求不满足契约的 Schema。参数：<c>id</c>、<c>reason</c>（第一条违规，细节 <c>violations</c> 列全部）。</summary>
    public const string SchemaViolation = "kernel.operation.schema_violation";

    /// <summary>请求对象的自校验失败。参数：<c>id</c>、<c>reason</c>（第一条违规，细节 <c>violations</c> 列全部）。</summary>
    public const string RequestInvalid = "kernel.operation.request_invalid";

    /// <summary>操作被限流。参数：<c>id</c>、<c>retryAfter</c>（秒）。</summary>
    public const string RateLimited = "kernel.resilience.rate_limited";

    /// <summary>操作已达并发上限。参数：<c>id</c>。</summary>
    public const string ConcurrencyExceeded = "kernel.resilience.concurrency_exceeded";

    /// <summary>操作的熔断器打开。参数：<c>id</c>、<c>retryAfter</c>（秒）。</summary>
    public const string CircuitOpen = "kernel.resilience.circuit_open";

    /// <summary>校验违规：嵌套过深。参数：<c>max</c>。</summary>
    public const string ValidationDepth = "kernel.validation.depth";

    /// <summary>校验违规：此处不允许任何值（<c>false</c> Schema）。</summary>
    public const string ValidationNotAllowed = "kernel.validation.not_allowed";

    /// <summary>校验违规：<c>$ref</c> 无法解析。参数：<c>reference</c>。</summary>
    public const string ValidationUnresolvedReference = "kernel.validation.unresolved_reference";

    /// <summary>校验违规：类型不符。参数：<c>expected</c>、<c>actual</c>。</summary>
    public const string ValidationType = "kernel.validation.type";

    /// <summary>校验违规：不符合任何一个 <c>anyOf</c> 候选。</summary>
    public const string ValidationAnyOf = "kernel.validation.any_of";

    /// <summary>校验违规：不恰好符合一个 <c>oneOf</c> 候选。</summary>
    public const string ValidationOneOf = "kernel.validation.one_of";

    /// <summary>校验违规：不在 <c>enum</c> 内。</summary>
    public const string ValidationEnum = "kernel.validation.enum";

    /// <summary>校验违规：不等于 <c>const</c>。参数：<c>expected</c>。</summary>
    public const string ValidationConst = "kernel.validation.const";

    /// <summary>校验违规：小于 <c>minimum</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMinimum = "kernel.validation.minimum";

    /// <summary>校验违规：大于 <c>maximum</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMaximum = "kernel.validation.maximum";

    /// <summary>校验违规：不大于 <c>exclusiveMinimum</c>。参数：<c>limit</c>。</summary>
    public const string ValidationExclusiveMinimum = "kernel.validation.exclusive_minimum";

    /// <summary>校验违规：不小于 <c>exclusiveMaximum</c>。参数：<c>limit</c>。</summary>
    public const string ValidationExclusiveMaximum = "kernel.validation.exclusive_maximum";

    /// <summary>校验违规：短于 <c>minLength</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMinLength = "kernel.validation.min_length";

    /// <summary>校验违规：长于 <c>maxLength</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMaxLength = "kernel.validation.max_length";

    /// <summary>校验违规：不匹配 <c>pattern</c>。参数：<c>pattern</c>。</summary>
    public const string ValidationPattern = "kernel.validation.pattern";

    /// <summary>校验违规：匹配 <c>pattern</c> 超时。参数：<c>pattern</c>。</summary>
    public const string ValidationPatternTimeout = "kernel.validation.pattern_timeout";

    /// <summary>校验违规：<c>pattern</c> 不是合法正则。参数：<c>pattern</c>。</summary>
    public const string ValidationPatternInvalid = "kernel.validation.pattern_invalid";

    /// <summary>校验违规：缺少必填属性。参数：<c>name</c>。</summary>
    public const string ValidationRequired = "kernel.validation.required";

    /// <summary>校验违规：属性未在契约中声明。</summary>
    public const string ValidationUndeclared = "kernel.validation.undeclared";

    /// <summary>校验违规：元素数少于 <c>minItems</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMinItems = "kernel.validation.min_items";

    /// <summary>校验违规：元素数多于 <c>maxItems</c>。参数：<c>limit</c>。</summary>
    public const string ValidationMaxItems = "kernel.validation.max_items";

    /// <summary>校验违规：请求对象自校验给出的结果没有文案时的兜底。</summary>
    public const string ValidationInvalid = "kernel.validation.invalid";

    /// <summary>健康探针失败。参数：<c>name</c>、<c>reason</c>。</summary>
    public const string HealthProbeFailed = "kernel.health.probe_failed";
}
