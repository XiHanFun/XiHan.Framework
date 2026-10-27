// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityCapabilities
{
    /// <summary>
    /// 认证：凭据到主体。
    /// </summary>
    public const string Authentication = "security.authentication";

    /// <summary>
    /// 授权：策略判定与权限授予。
    /// </summary>
    public const string Authorization = "security.authorization";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
