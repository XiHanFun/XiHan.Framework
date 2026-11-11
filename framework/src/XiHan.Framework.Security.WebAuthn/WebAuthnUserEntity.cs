// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册选项里的用户（<c>PublicKeyCredentialUserEntityJSON</c>）
/// </summary>
/// <remarks>
/// 用户句柄由应用给出，1 到 64 字节、不含个人信息；可发现凭据登录时认证器把它原样交回，应用据此找到用户
/// </remarks>
/// <param name="Id">用户句柄（base64url）</param>
/// <param name="Name">账号名，认证器在选择凭据时显示</param>
/// <param name="DisplayName">显示名</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnUserEntity(string Id, string Name, string DisplayName);
