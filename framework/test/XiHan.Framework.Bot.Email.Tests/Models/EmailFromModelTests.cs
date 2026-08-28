// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Bot.Email.Models;

namespace XiHan.Framework.Bot.Email.Tests.Models;

/// <summary>
/// <see cref="EmailFromModel"/> 默认值与可变性测试
/// </summary>
/// <remarks>
/// 发件人模型的默认值全部与安全相关：端口 587、UseSsl=true、UTF8 编码、
/// AcceptInvalidCertificate=false。尤其最后一项默认必须为 false，
/// 否则 EmailBot 会用恒真回调替换 TLS 证书校验，生产环境静默失去中间人防护。
/// </remarks>
public class EmailFromModelTests
{
    /// <summary>
    /// 字符串字段默认为空串而非 null
    /// </summary>
    [Fact]
    public void StringDefaults_AreEmptyNotNull()
    {
        var model = new EmailFromModel();

        Assert.Equal(string.Empty, model.SmtpHost);
        Assert.Equal(string.Empty, model.FromMail);
        Assert.Equal(string.Empty, model.FromPassword);
        Assert.Equal(string.Empty, model.FromUserName);
        Assert.Equal(string.Empty, model.FromName);
    }

    /// <summary>
    /// 默认端口 587 且默认启用 SSL
    /// </summary>
    [Fact]
    public void Defaults_UseSubmissionPortWithSsl()
    {
        var model = new EmailFromModel();

        Assert.Equal(587, model.SmtpPort);
        Assert.True(model.UseSsl);
    }

    /// <summary>
    /// 默认内容编码为 UTF8
    /// </summary>
    [Fact]
    public void Coding_DefaultsToUtf8()
    {
        var model = new EmailFromModel();

        Assert.Same(Encoding.UTF8, model.Coding);
    }

    /// <summary>
    /// 默认不接受无效/自签证书
    /// </summary>
    [Fact]
    public void AcceptInvalidCertificate_DefaultsToFalse()
    {
        var model = new EmailFromModel();

        Assert.False(model.AcceptInvalidCertificate);
    }

    /// <summary>
    /// 所有属性均可写
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var model = new EmailFromModel
        {
            SmtpHost = "smtp.example.com",
            SmtpPort = 465,
            UseSsl = false,
            FromMail = "from@example.com",
            FromPassword = "secret",
            FromUserName = "from@example.com",
            FromName = "曦寒",
            Coding = Encoding.ASCII,
            AcceptInvalidCertificate = true
        };

        Assert.Equal("smtp.example.com", model.SmtpHost);
        Assert.Equal(465, model.SmtpPort);
        Assert.False(model.UseSsl);
        Assert.Equal("from@example.com", model.FromMail);
        Assert.Equal("secret", model.FromPassword);
        Assert.Equal("from@example.com", model.FromUserName);
        Assert.Equal("曦寒", model.FromName);
        Assert.Same(Encoding.ASCII, model.Coding);
        Assert.True(model.AcceptInvalidCertificate);
    }
}
