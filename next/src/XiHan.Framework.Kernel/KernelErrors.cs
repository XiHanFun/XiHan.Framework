// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel;

/// <summary>
/// 内核错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class KernelErrors
{
    /// <summary>能力解析失败：缺失依赖。</summary>
    public const string MissingCapability = "KERNEL_CAPABILITY_001";

    /// <summary>能力解析失败：循环依赖。</summary>
    public const string CircularCapability = "KERNEL_CAPABILITY_002";

    /// <summary>能力解析失败：版本约束冲突。</summary>
    public const string CapabilityVersionConflict = "KERNEL_CAPABILITY_003";

    /// <summary>同一能力有多个候选且未指定。</summary>
    public const string AmbiguousProvider = "KERNEL_CAPABILITY_004";

    /// <summary>清单 ID 重复。</summary>
    public const string DuplicateManifest = "KERNEL_CAPABILITY_005";

    /// <summary>清单文件不合法。</summary>
    public const string InvalidManifest = "KERNEL_CAPABILITY_006";

    /// <summary>中间件顺序约束成环。</summary>
    public const string PipelineCycle = "KERNEL_PIPELINE_001";

    /// <summary>阶段名未知。</summary>
    public const string UnknownStage = "KERNEL_PIPELINE_002";

    /// <summary>中间件已注册或约束引用了未注册的中间件。</summary>
    public const string PipelineRegistration = "KERNEL_PIPELINE_003";

    /// <summary>必需绑定缺失。</summary>
    public const string MissingBinding = "KERNEL_HOSTING_001";

    /// <summary>特性启动失败。</summary>
    public const string FeatureStartFailed = "KERNEL_HOSTING_002";

    /// <summary>特性停止超时。</summary>
    public const string FeatureStopTimeout = "KERNEL_HOSTING_003";

    /// <summary>应用状态不允许该操作。</summary>
    public const string InvalidAppState = "KERNEL_HOSTING_004";

    /// <summary>应用未就绪或正在停止，暂不接收调用；协议适配器据此返回 503。</summary>
    public const string NotServing = "KERNEL_UNAVAILABLE_001";

    /// <summary>调用超过截止时间。</summary>
    public const string DeadlineExceeded = "KERNEL_TIMEOUT_001";

    /// <summary>调用被取消。</summary>
    public const string Cancelled = "KERNEL_CANCELLED_001";

    /// <summary>调用期间发生未处理异常。</summary>
    public const string Unexpected = "KERNEL_UNEXPECTED_001";

    /// <summary>权限码重复。</summary>
    public const string DuplicatePermission = "KERNEL_SECURITY_001";

    /// <summary>权限注册表已满。</summary>
    public const string PermissionRegistryFull = "KERNEL_SECURITY_002";

    /// <summary>操作未登记。</summary>
    public const string UnknownOperation = "KERNEL_NOTFOUND_001";

    /// <summary>操作载荷不合法。</summary>
    public const string InvalidOperationPayload = "KERNEL_VALIDATION_001";

    /// <summary>类型化调用的请求或响应类型与登记不符。</summary>
    public const string OperationTypeMismatch = "KERNEL_VALIDATION_002";

    /// <summary>请求不满足操作契约的 JSON Schema；Validate 阶段拒绝，不进 Handle。</summary>
    public const string SchemaViolation = "KERNEL_VALIDATION_003";

    /// <summary>请求对象的自校验（<c>IValidatableObject</c>）失败；Validate 阶段拒绝，不进 Handle。</summary>
    public const string RequestInvalid = "KERNEL_VALIDATION_004";

    /// <summary>操作 ID 重复登记。</summary>
    public const string DuplicateOperation = "KERNEL_OPERATION_001";

    /// <summary>操作登记表已满。</summary>
    public const string OperationRegistryFull = "KERNEL_OPERATION_002";
}
