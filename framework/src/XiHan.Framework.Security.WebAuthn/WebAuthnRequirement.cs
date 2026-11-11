// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 依赖方对用户验证或可发现凭据的要求程度
/// </summary>
/// <remarks>
/// 对应 WebAuthn 的 <c>required</c>、<c>preferred</c>、<c>discouraged</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum WebAuthnRequirement
{
    /// <summary>
    /// 必须
    /// </summary>
    Required = 0,

    /// <summary>
    /// 尽量
    /// </summary>
    Preferred = 1,

    /// <summary>
    /// 不要
    /// </summary>
    Discouraged = 2,
}
