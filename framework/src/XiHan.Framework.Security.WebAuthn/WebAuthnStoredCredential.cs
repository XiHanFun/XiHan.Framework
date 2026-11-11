// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 应用存储的凭据，认证时交给核对
/// </summary>
/// <param name="CredentialId">凭据 ID（base64url）</param>
/// <param name="PublicKey">注册时得到的 COSE 公钥（base64url）</param>
/// <param name="SignCount">上次的签名计数</param>
/// <param name="UserHandle">凭据所属用户的句柄（base64url）；给出时，响应带的 <c>userHandle</c> 必须与它相同</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnStoredCredential(string CredentialId, string PublicKey, uint SignCount, string? UserHandle);
