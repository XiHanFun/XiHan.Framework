// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 依赖方接受的一种公钥算法（<c>PublicKeyCredentialParameters</c>）
/// </summary>
/// <param name="Type">凭据类型，恒为 <c>public-key</c></param>
/// <param name="Alg">COSE 算法编号</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnCredentialParameter(string Type, int Alg);
