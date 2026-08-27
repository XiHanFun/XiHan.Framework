// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.Hosting;

namespace XiHan.Framework.Core.Tests.Extensions.Hosting;

/// <summary>
/// 曦寒宿主环境扩展方法测试
/// </summary>
/// <remarks>
/// 三个具名判定都转调 <c>IsEnvironment</c>，而它用的是<b>忽略大小写</b>的比较——
/// 环境名可能来自环境变量、命令行、配置文件，大小写不受控，一旦改成区分大小写，
/// 生产环境上一个 "production" 就会让所有 IsProduction 分支失效。用例把这条逐项锁死。
/// 环境名固定用字面量而不是常量引用，这样常量本身被改动时用例也会红。
/// </remarks>
public class HostEnvironmentExtensionsTests
{
    /// <summary>
    /// 三个具名判定按环境名精确落位
    /// </summary>
    /// <param name="environmentName">环境名</param>
    /// <param name="isDevelopment">是否开发环境</param>
    /// <param name="isStaging">是否预发环境</param>
    /// <param name="isProduction">是否生产环境</param>
    [Theory]
    [InlineData("Development", true, false, false)]
    [InlineData("Staging", false, true, false)]
    [InlineData("Production", false, false, true)]
    [InlineData("Testing", false, false, false)]
    public void NamedChecks_MatchEnvironmentName(string environmentName, bool isDevelopment, bool isStaging, bool isProduction)
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment { EnvironmentName = environmentName };

        Assert.Equal(isDevelopment, environment.IsDevelopment());
        Assert.Equal(isStaging, environment.IsStaging());
        Assert.Equal(isProduction, environment.IsProduction());
    }

    /// <summary>
    /// 判定忽略大小写
    /// </summary>
    /// <param name="environmentName">大小写各异的环境名</param>
    [Theory]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    [InlineData("DeVeLoPmEnT")]
    public void IsDevelopment_IgnoresCase(string environmentName)
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment { EnvironmentName = environmentName };

        Assert.True(environment.IsDevelopment());
    }

    /// <summary>
    /// 任意环境名都能通过通用判定命中
    /// </summary>
    [Fact]
    public void IsEnvironment_MatchesArbitraryName()
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment { EnvironmentName = "灰度" };

        Assert.True(environment.IsEnvironment("灰度"));
        Assert.False(environment.IsEnvironment("生产"));
    }

    /// <summary>
    /// 环境名未设置时三个具名判定全部为假
    /// </summary>
    /// <remarks>
    /// 应用装配末尾会把空环境名兜底成 Production，但在兜底之前读到的必须是"哪个都不是"，
    /// 否则会出现"还没设环境就已经被当成开发环境"这种最危险的默认值。
    /// </remarks>
    [Fact]
    public void NamedChecks_WhenEnvironmentNameIsNull_AreAllFalse()
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment();

        Assert.False(environment.IsDevelopment());
        Assert.False(environment.IsStaging());
        Assert.False(environment.IsProduction());
    }

    /// <summary>
    /// 宿主环境为空时四个扩展都抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void AllChecks_WhenHostEnvironmentIsNull_ThrowArgumentNullException()
    {
        IXiHanHostEnvironment environment = null!;

        Assert.Equal("hostEnvironment", Assert.Throws<ArgumentNullException>(() => environment.IsDevelopment()).ParamName);
        Assert.Equal("hostEnvironment", Assert.Throws<ArgumentNullException>(() => environment.IsStaging()).ParamName);
        Assert.Equal("hostEnvironment", Assert.Throws<ArgumentNullException>(() => environment.IsProduction()).ParamName);
        Assert.Equal("hostEnvironment", Assert.Throws<ArgumentNullException>(() => environment.IsEnvironment("Development")).ParamName);
    }

    /// <summary>
    /// 环境名在运行期被改写后判定结果随之变化
    /// </summary>
    /// <remarks>
    /// 环境名在接口上可写，框架的兜底逻辑正是靠改写它生效的，
    /// 因此扩展方法必须每次都重新读属性，不能缓存首次读到的值。
    /// </remarks>
    [Fact]
    public void NamedChecks_FollowLaterEnvironmentNameChanges()
    {
        IXiHanHostEnvironment environment = new XiHanHostEnvironment { EnvironmentName = "Development" };

        Assert.True(environment.IsDevelopment());

        environment.EnvironmentName = "Production";

        Assert.False(environment.IsDevelopment());
        Assert.True(environment.IsProduction());
    }
}
