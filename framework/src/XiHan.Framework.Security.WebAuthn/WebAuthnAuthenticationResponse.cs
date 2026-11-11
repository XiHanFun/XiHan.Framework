// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 浏览器交回的认证响应（<c>AuthenticationResponseJSON</c>）
/// </summary>
/// <remarks>
/// 浏览器侧取 <c>credential.toJSON()</c>；<c>authenticatorAttachment</c> 与 <c>clientExtensionResults</c> 不读
/// </remarks>
/// <param name="Id">凭据 ID（base64url）</param>
/// <param name="RawId">凭据 ID（base64url），与 <paramref name="Id"/> 相同</param>
/// <param name="Type">凭据类型，必须是 <c>public-key</c></param>
/// <param name="Response">认证器的部分</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnAuthenticationResponse(string Id, string RawId, string Type, WebAuthnAssertionResponse Response);
