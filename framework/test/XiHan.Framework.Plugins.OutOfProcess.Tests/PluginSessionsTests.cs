// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 会话令牌的签发、查找与作废
/// </summary>
/// <remarks>
/// 宿主按令牌的散列找会话、再与签发的令牌做常数时间比较（<c>handshake.md</c> §4，ADR-0325）。比较是否常数时间测不出来，这里核对的是可观察的结果：签发的令牌找得到自己的运行时，错的、只差一个字符的、长短不同的与作废之后的令牌都找不到
/// </remarks>
public sealed class PluginSessionsTests
{
    [Fact]
    public void Issue_GivesEachRuntimeItsOwnToken_ThatResolvesToIt()
    {
        var sessions = new PluginSessions();
        using var first = Runtime("xihan.test.first", sessions);
        using var second = Runtime("xihan.test.second", sessions);

        var firstToken = sessions.Issue(first);
        var secondToken = sessions.Issue(second);

        // 32 字节随机数的 base64url，不带填充
        Assert.Equal(43, firstToken.Length);
        Assert.NotEqual(firstToken, secondToken);
        Assert.True(sessions.TryResolve(firstToken, out var resolved));
        Assert.Same(first, resolved);
        Assert.True(sessions.TryResolve(secondToken, out resolved));
        Assert.Same(second, resolved);
    }

    [Fact]
    public void WrongTokens_DoNotResolve()
    {
        var sessions = new PluginSessions();
        using var runtime = Runtime("xihan.test.callbacks", sessions);
        var token = sessions.Issue(runtime);

        foreach (var wrong in new[] { null, string.Empty, "wrong", token[..^1], token + "A", SwapCase(token), " " + token, token + "\uD800" })
        {
            Assert.False(sessions.TryResolve(wrong, out var resolved), $"Token '{wrong}' must not resolve.");
            Assert.Null(resolved);
        }
    }

    [Fact]
    public void ATokenThatDiffersInOneCharacter_DoesNotResolve_WhereverTheDifferenceIs()
    {
        var sessions = new PluginSessions();
        using var runtime = Runtime("xihan.test.callbacks", sessions);
        var token = sessions.Issue(runtime);

        for (var index = 0; index < token.Length; index++)
        {
            // 换成 base64url 字母表里的另一个字符：长度相同、只差这一处
            var characters = token.ToCharArray();
            characters[index] = characters[index] == 'A' ? 'B' : 'A';
            Assert.False(sessions.TryResolve(new string(characters), out _), $"A token differing at index {index} must not resolve.");
        }

        Assert.True(sessions.TryResolve(token, out _));
    }

    [Fact]
    public void Revoke_MakesOnlyThatTokenUnresolvable()
    {
        var sessions = new PluginSessions();
        using var first = Runtime("xihan.test.first", sessions);
        using var second = Runtime("xihan.test.second", sessions);
        var firstToken = sessions.Issue(first);
        var secondToken = sessions.Issue(second);

        // 作废不认识的、只差一个字符的与空的令牌：什么都不动
        sessions.Revoke(null);
        sessions.Revoke("wrong");
        sessions.Revoke(firstToken[..^1] + (firstToken[^1] == 'A' ? 'B' : 'A'));
        Assert.True(sessions.TryResolve(firstToken, out _));

        sessions.Revoke(firstToken);

        Assert.False(sessions.TryResolve(firstToken, out var revoked));
        Assert.Null(revoked);
        Assert.True(sessions.TryResolve(secondToken, out var kept));
        Assert.Same(second, kept);

        // 重复作废照旧什么都不动；重启（进程结束、再次签发）换新令牌，旧令牌仍找不到
        sessions.Revoke(firstToken);
        var restarted = sessions.Issue(first);
        Assert.NotEqual(firstToken, restarted);
        Assert.False(sessions.TryResolve(firstToken, out _));
        Assert.True(sessions.TryResolve(restarted, out var again));
        Assert.Same(first, again);
    }

    /// <summary>
    /// 把字母的大小写对调：令牌按序数比较，换了大小写的就是另一个令牌
    /// </summary>
    private static string SwapCase(string text)
        => string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsUpper(source[i]) ? char.ToLowerInvariant(source[i]) : char.ToUpperInvariant(source[i]);
            }
        });

    /// <summary>
    /// 一个不启动进程、清单不声明任何权限的运行时
    /// </summary>
    private static PluginRuntime Runtime(string id, PluginSessions sessions)
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = OutOfProcessPluginOptions.FromConfiguration(configuration);
        var context = new PluginHostContext([], 1, "en-US", "tests", Path.GetTempPath(), options, configuration, null, TimeProvider.System, sessions, TimeSpan.FromSeconds(5));
        return new PluginRuntime(TestManifests.Create(id), Path.GetTempPath(), string.Empty, new PluginPermissions([]), context, NullLogger.Instance);
    }
}
