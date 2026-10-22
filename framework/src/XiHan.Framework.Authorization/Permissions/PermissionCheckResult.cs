// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 带请求上下文的权限判定结果
/// </summary>
/// <param name="IsGranted">是否授予</param>
/// <param name="FailureCode">未授予的原因码（见 <see cref="PermissionFailureCodes"/>）</param>
/// <param name="FailureMessage">未授予时给调用方看的说明（不含条件细节）</param>
public sealed record PermissionCheckResult(bool IsGranted, string? FailureCode = null, string? FailureMessage = null)
{
    /// <summary>
    /// 授予
    /// </summary>
    public static PermissionCheckResult Granted { get; } = new(true);

    /// <summary>
    /// 没有任何授予
    /// </summary>
    public static PermissionCheckResult NotGranted { get; } = new(false, PermissionFailureCodes.NotGranted, "权限不足");

    /// <summary>
    /// 有授予但附加条件不满足，或有条件的禁止命中
    /// </summary>
    public static PermissionCheckResult ConditionNotMet { get; } = new(false, PermissionFailureCodes.ConditionNotMet, "当前条件下不允许此操作");
}
