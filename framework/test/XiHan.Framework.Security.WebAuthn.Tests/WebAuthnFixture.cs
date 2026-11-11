// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Text;
using System.Security.Cryptography;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.WebAuthn.Tests;

/// <summary>
/// 各用例共用的依赖方与断言
/// </summary>
internal static class WebAuthnFixture
{
    /// <summary>
    /// 依赖方 ID
    /// </summary>
    public const string RelyingPartyId = "example.com";

    /// <summary>
    /// 允许的来源
    /// </summary>
    public const string Origin = "https://example.com";

    /// <summary>
    /// 缺省选项：要求用户验证与可发现凭据
    /// </summary>
    public static WebAuthnOptions Options { get; } = new() { RelyingPartyId = RelyingPartyId, Origins = [Origin, "https://login.example.com"] };

    /// <summary>
    /// 按缺省选项的仪式
    /// </summary>
    public static WebAuthnCeremonies Ceremonies { get; } = new(Options);

    /// <summary>
    /// 新挑战（base64url）
    /// </summary>
    public static string NewChallenge() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// 对缺省依赖方合法的一次仪式
    /// </summary>
    public static WebAuthnTestCeremony Ceremony(string challenge) => new() { Challenge = challenge, Origin = Origin, RelyingPartyId = RelyingPartyId };

    /// <summary>
    /// 断言失败的错误码与原因
    /// </summary>
    public static void AssertFailure<T>(Result<T> result, string code, string? reason)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.Equal(code, result.Error!.Code);
        if (reason is not null)
        {
            Assert.Equal(reason, result.Error.Details?["reason"]);
        }
    }
}
