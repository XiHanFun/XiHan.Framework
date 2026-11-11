// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.WebAuthn.Tests;

public sealed class WebAuthnOptionsTests
{
    /// <summary>
    /// 由键值组出配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(WebAuthnOptions.ConfigurationSection + ":" + v.Key, v.Value))).Build();

    [Fact]
    public void Configuration_IsRead_AndOriginsAreNormalized()
    {
        var options = WebAuthnOptions.FromConfiguration(Configuration(
            ("RelyingPartyId", "Example.COM"),
            ("RelyingPartyName", "Example"),
            ("Origins:0", "HTTPS://Example.com:443/"),
            ("Origins:1", "https://login.example.com:8443"),
            ("Origins:2", "https://example.com"),
            ("UserVerification", "preferred"),
            ("ResidentKey", "Discouraged"),
            ("TimeoutSeconds", "120"),
            ("ChallengeSize", "48")));

        Assert.Equal("example.com", options.RelyingPartyId);
        Assert.Equal(["https://example.com", "https://login.example.com:8443"], options.Origins);
        Assert.Equal((WebAuthnRequirement.Preferred, WebAuthnRequirement.Discouraged, TimeSpan.FromMinutes(2), 48), (options.UserVerification, options.ResidentKey, options.Timeout, options.ChallengeSize));
    }

    [Fact]
    public void Defaults_RequireVerificationAndDiscoverableCredentials()
    {
        var options = WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "localhost"), ("Origins:0", "http://localhost:9100")));

        Assert.Equal((WebAuthnRequirement.Required, WebAuthnRequirement.Required, TimeSpan.FromMinutes(5), 32, null), (options.UserVerification, options.ResidentKey, options.Timeout, options.ChallengeSize, options.RelyingPartyName));
        Assert.Equal(["http://localhost:9100"], options.Origins);
    }

    [Theory]
    [InlineData("https://example.com/login")]
    [InlineData("https://example.com?x=1")]
    [InlineData("http://example.com")]
    [InlineData("https://evil.com")]
    [InlineData("https://notexample.com")]
    [InlineData("example.com")]
    public void BadOrigins_FailAtStartup(string origin)
        => Assert.Throws<ArgumentException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "example.com"), ("Origins:0", origin))));

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com:443")]
    [InlineData("example.com/")]
    [InlineData("192.168.0.1")]
    [InlineData(" ")]
    public void BadRelyingPartyIds_AreRejected(string id)
        => Assert.Throws<ArgumentException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", id), ("Origins:0", "https://example.com"))));

    [Fact]
    public void MissingOrInvalidValues_FailAtStartup()
    {
        Assert.Throws<ArgumentException>(() => WebAuthnOptions.FromConfiguration(Configuration(("Origins:0", "https://example.com"))));
        Assert.Throws<ArgumentException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "example.com"))));
        Assert.Throws<ArgumentException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "example.com"), ("Origins:0", "https://example.com"), ("UserVerification", "1"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "example.com"), ("Origins:0", "https://example.com"), ("ChallengeSize", "8"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => WebAuthnOptions.FromConfiguration(Configuration(("RelyingPartyId", "example.com"), ("Origins:0", "https://example.com"), ("TimeoutSeconds", "0"))));
        // 直接构造时也核对来源与 RP ID 配套
        Assert.Throws<ArgumentException>(() => new WebAuthnCeremonies(new WebAuthnOptions { RelyingPartyId = "example.com", Origins = ["https://example.org"] }));
    }

    [Fact]
    public async Task Feature_RegistersTheCeremonies_FromConfiguration()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<WebAuthnSecurityFeature>(),
            [new("XiHan:Security:WebAuthn:RelyingPartyId", "example.com"), new("XiHan:Security:WebAuthn:Origins:0", "https://example.com")]);

        var ceremonies = host.Services.GetRequiredService<WebAuthnCeremonies>();
        Assert.Equal("example.com", ceremonies.Options.RelyingPartyId);
        Assert.Same(ceremonies.Options, host.Services.GetRequiredService<WebAuthnOptions>());
    }
}
