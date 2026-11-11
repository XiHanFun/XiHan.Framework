// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 认证核对通过的结果
/// </summary>
/// <remarks>
/// 应用用 <see cref="SignCount"/> 与 <see cref="BackedUp"/> 更新存储的凭据
/// </remarks>
/// <param name="CredentialId">凭据 ID（base64url）</param>
/// <param name="SignCount">新的签名计数</param>
/// <param name="UserVerified">本次是否做过用户验证</param>
/// <param name="BackedUp">凭据是否已同步备份（BS）</param>
/// <param name="UserHandle">凭据所属用户的句柄（base64url，取自存储的凭据）：免用户名的流程据此确定是谁登录，不取响应里的句柄（ADR-0179）</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnVerifiedAssertion(string CredentialId, uint SignCount, bool UserVerified, bool BackedUp, string UserHandle);
