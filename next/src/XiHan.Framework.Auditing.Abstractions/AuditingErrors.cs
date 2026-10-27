// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计子系统的错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AuditingErrors
{
    /// <summary>
    /// 查询不合法：页大小越界、起止颠倒或续页令牌不可识别。
    /// </summary>
    public const string QueryInvalid = "AUDITING_VALIDATION_001";

    /// <summary>
    /// 审计存储不可达。
    /// </summary>
    public const string TrailUnavailable = "AUDITING_UNAVAILABLE_001";
}
