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
}
