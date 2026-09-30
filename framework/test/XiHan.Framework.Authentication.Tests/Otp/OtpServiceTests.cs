// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using XiHan.Framework.Authentication.Otp;

namespace XiHan.Framework.Authentication.Tests.Otp;

/// <summary>
/// OTP 服务测试
/// </summary>
public class OtpServiceTests
{
    /// <summary>
    /// 批量生成的恢复码全部为 XXXX-XXXX 格式的大写字母与数字
    /// </summary>
    [Fact]
    public void GenerateRecoveryCodes_Many_AllMatchFormat()
    {
        var service = new OtpService(Microsoft.Extensions.Options.Options.Create(new OtpOptions()));

        var codes = service.GenerateRecoveryCodes(20000);

        Assert.Equal(20000, codes.Count);
        Assert.All(codes, code => Assert.Matches(new Regex("^[A-Z0-9]{4}-[A-Z0-9]{4}$"), code));
    }
}
