// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册选项里的依赖方（<c>PublicKeyCredentialRpEntity</c>）
/// </summary>
/// <param name="Id">依赖方 ID</param>
/// <param name="Name">认证器上显示的名称</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnRelyingParty(string Id, string Name);
