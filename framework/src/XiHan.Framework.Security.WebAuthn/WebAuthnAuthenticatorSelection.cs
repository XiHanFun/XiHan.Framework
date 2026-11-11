// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册选项里对认证器的要求（<c>AuthenticatorSelectionCriteria</c>）
/// </summary>
/// <param name="ResidentKey">可发现凭据：<c>required</c>、<c>preferred</c> 或 <c>discouraged</c></param>
/// <param name="RequireResidentKey">与 <paramref name="ResidentKey"/> 为 <c>required</c> 同义，给只认 Level 1 的浏览器</param>
/// <param name="UserVerification">用户验证：<c>required</c>、<c>preferred</c> 或 <c>discouraged</c></param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnAuthenticatorSelection(string ResidentKey, bool RequireResidentKey, string UserVerification);
