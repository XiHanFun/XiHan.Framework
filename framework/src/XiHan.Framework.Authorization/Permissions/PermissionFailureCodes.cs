// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 权限判定的失败原因码
/// </summary>
public static class PermissionFailureCodes
{
    /// <summary>
    /// 没有任何授予
    /// </summary>
    public const string NotGranted = "permission.not_granted";

    /// <summary>
    /// 有授予但附加条件不满足，或有条件的禁止命中
    /// </summary>
    public const string ConditionNotMet = "permission.condition_not_met";

    /// <summary>
    /// 端点声明的 ABAC 策略不通过
    /// </summary>
    public const string AbacDenied = "permission.abac_denied";
}
