// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 权限判定的失败原因（随 403 写出原因码与说明）
/// </summary>
public sealed class PermissionAuthorizationFailureReason : AuthorizationFailureReason
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="handler">产生失败的授权处理器</param>
    /// <param name="code">原因码（见 <see cref="Permissions.PermissionFailureCodes"/>）</param>
    /// <param name="message">给调用方看的说明</param>
    public PermissionAuthorizationFailureReason(IAuthorizationHandler handler, string code, string message)
        : base(handler, message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>
    /// 原因码
    /// </summary>
    public string Code { get; }
}
