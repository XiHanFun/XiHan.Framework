// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.OpenIdConnect;

/// <summary>
/// 一次取得的签发者元数据：发现文档里的签发者与 JWKS 地址，以及 JWKS 里的签名密钥。
/// </summary>
/// <param name="Issuer">发现文档的 <c>issuer</c>。</param>
/// <param name="JwksUri">发现文档的 <c>jwks_uri</c>。</param>
/// <param name="SigningKeys">JWKS 里可用于验签的密钥。</param>
/// <param name="RetrievedAt">取得时刻。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenIdConnectMetadata(string Issuer, Uri JwksUri, IReadOnlyList<SecurityKey> SigningKeys, DateTimeOffset RetrievedAt);
