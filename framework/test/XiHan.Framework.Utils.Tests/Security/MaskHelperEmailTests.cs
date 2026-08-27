// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 邮箱脱敏回归测试
/// </summary>
/// <remarks>
/// <para>
/// 修复前 <see cref="MaskHelper.MaskEmail"/> 从 <c>RegexHelper.EmailRegex()</c> 的第 1/2/3 个捕获组
/// 取用户名、域名主体、后缀，而那个正则通篇没有捕获组，三个 <c>Groups[n].Value</c> 全是空串，
/// 于是任何合法邮箱都被脱敏成 <c>"@."</c>——脱敏本该保留可辨识的轮廓，结果把信息全丢了。
/// </para>
/// <para>
/// 现在切分由方法自己按 @ 与最后一个 . 完成，正则只负责判定是否合法邮箱。
/// 下面既钉死具体输出，也钉死「@ / 域名 / 顶级后缀必须原样保留」这条不变量。
/// </para>
/// </remarks>
public class MaskHelperEmailTests
{
    /// <summary>
    /// 合法邮箱按用户名与域名主体分别遮蔽中段，@ 与顶级后缀原样保留
    /// </summary>
    [Theory]
    [InlineData("test@example.com", "t**t@e**mple.com")]
    [InlineData("ab@qq.com", "a*@q*.com")]
    [InlineData("user@mail.example.co.uk", "u**r@m**l.example.co.uk")]
    public void MaskEmail_MasksLocalPartAndDomainBody(string email, string expected)
    {
        Assert.Equal(expected, MaskHelper.MaskEmail(email));
    }

    /// <summary>
    /// 脱敏结果绝不能退化成 "@." —— 这是修复前的典型症状
    /// </summary>
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("ab@qq.com")]
    [InlineData("a@qq.com")]
    [InlineData("zhaifanhua@xihanfun.com")]
    [InlineData("user.name+tag@sub.domain.org")]
    public void MaskEmail_NeverCollapsesToAtDot(string email)
    {
        var masked = MaskHelper.MaskEmail(email);

        Assert.NotEqual("@.", masked);
        Assert.NotEqual(email, masked);
    }

    /// <summary>
    /// 域名与顶级后缀必须原样出现在脱敏结果里
    /// </summary>
    [Theory]
    [InlineData("test@example.com", "com")]
    [InlineData("user@mail.example.co.uk", "uk")]
    [InlineData("zhaifanhua@xihanfun.cn", "cn")]
    public void MaskEmail_KeepsAtSignAndTopLevelSuffix(string email, string suffix)
    {
        var masked = MaskHelper.MaskEmail(email);

        Assert.Equal(1, masked.Count(c => c == '@'));
        Assert.EndsWith("." + suffix, masked);
        Assert.Contains("*", masked);
    }

    /// <summary>
    /// 多级域名只把最后一段当作顶级后缀，中间层级留在域名主体里
    /// </summary>
    [Fact]
    public void MaskEmail_WithMultiLevelDomain_TreatsOnlyLastLabelAsSuffix()
    {
        var masked = MaskHelper.MaskEmail("user@mail.example.co.uk");

        Assert.EndsWith(".co.uk", masked);
        Assert.Contains("example", masked);
    }

    /// <summary>
    /// 不是合法邮箱时原样返回，不做任何遮蔽
    /// </summary>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    [InlineData("no-dot@localhost")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("")]
    public void MaskEmail_WhenNotValidEmail_ReturnsInputUnchanged(string email)
    {
        Assert.Equal(email, MaskHelper.MaskEmail(email));
    }

    /// <summary>
    /// 同一邮箱多次脱敏结果稳定
    /// </summary>
    [Fact]
    public void MaskEmail_IsIdempotentAcrossCalls()
    {
        Assert.Equal(MaskHelper.MaskEmail("test@example.com"), MaskHelper.MaskEmail("test@example.com"));
    }
}
