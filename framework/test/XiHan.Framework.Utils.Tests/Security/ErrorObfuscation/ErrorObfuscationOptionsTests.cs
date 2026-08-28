// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Options;

namespace XiHan.Framework.Utils.Tests.Security.ErrorObfuscation;

/// <summary>
/// 错误混淆配置选项测试
/// </summary>
/// <remarks>
/// 两个枚举的数值被 <c>(ErrorFormat)random.Next(0, 5)</c> 这类整数强转直接依赖，
/// 顺序一改随机分布就跟着变，因此枚举取值必须锁死。
/// </remarks>
public class ErrorObfuscationOptionsTests
{
    /// <summary>
    /// 错误格式枚举的数值不可漂移
    /// </summary>
    [Fact]
    public void ErrorFormat_HasStableNumericValues()
    {
        Assert.Equal(0, (int)ErrorFormat.JsonObject);
        Assert.Equal(1, (int)ErrorFormat.JsonArray);
        Assert.Equal(2, (int)ErrorFormat.PlainText);
        Assert.Equal(3, (int)ErrorFormat.Xml);
        Assert.Equal(4, (int)ErrorFormat.Html);
        Assert.Equal(5, Enum.GetValues<ErrorFormat>().Length);
    }

    /// <summary>
    /// 编程语言枚举的数值不可漂移
    /// </summary>
    [Fact]
    public void ProgrammingLanguage_HasStableNumericValues()
    {
        Assert.Equal(0, (int)ProgrammingLanguage.CSharp);
        Assert.Equal(1, (int)ProgrammingLanguage.Java);
        Assert.Equal(2, (int)ProgrammingLanguage.Php);
        Assert.Equal(3, (int)ProgrammingLanguage.Go);
        Assert.Equal(4, (int)ProgrammingLanguage.Python);
        Assert.Equal(5, (int)ProgrammingLanguage.NodeJs);
        Assert.Equal(6, (int)ProgrammingLanguage.Ruby);
        Assert.Equal(7, (int)ProgrammingLanguage.Rust);
        Assert.Equal(8, Enum.GetValues<ProgrammingLanguage>().Length);
    }

    /// <summary>
    /// 默认配置是「全随机 + 不延迟」，随机延迟区间为 100~2000 毫秒
    /// </summary>
    [Fact]
    public void Default_UsesFullyRandomNoDelaySettings()
    {
        var options = ErrorObfuscationOptions.Default;

        Assert.Null(options.Language);
        Assert.Null(options.Format);
        Assert.Equal(0, options.StatusCode);
        Assert.Equal(0, options.DelayMs);
        Assert.False(options.RandomDelay);
        Assert.Equal(100, options.MinDelayMs);
        Assert.Equal(2000, options.MaxDelayMs);
    }

    /// <summary>
    /// 每次读取默认配置都是新实例，改一份不影响另一份
    /// </summary>
    [Fact]
    public void Default_ReturnsFreshInstanceEachTime()
    {
        var first = ErrorObfuscationOptions.Default;
        first.DelayMs = 500;

        Assert.NotSame(first, ErrorObfuscationOptions.Default);
        Assert.Equal(0, ErrorObfuscationOptions.Default.DelayMs);
    }

    /// <summary>
    /// 随机延迟工厂方法只开随机延迟并覆盖区间
    /// </summary>
    [Fact]
    public void WithRandomDelay_SetsRangeAndFlag()
    {
        var options = ErrorObfuscationOptions.WithRandomDelay(50, 300);

        Assert.True(options.RandomDelay);
        Assert.Equal(50, options.MinDelayMs);
        Assert.Equal(300, options.MaxDelayMs);
        Assert.Equal(0, options.DelayMs);
        Assert.Null(options.Format);
        Assert.Null(options.Language);
    }

    /// <summary>
    /// 随机延迟工厂方法的默认区间与选项默认值一致
    /// </summary>
    [Fact]
    public void WithRandomDelay_ByDefault_KeepsStandardRange()
    {
        var options = ErrorObfuscationOptions.WithRandomDelay();

        Assert.Equal(100, options.MinDelayMs);
        Assert.Equal(2000, options.MaxDelayMs);
    }

    /// <summary>
    /// 指定格式的工厂方法只设置格式
    /// </summary>
    [Fact]
    public void WithFormat_OnlySetsFormat()
    {
        var options = ErrorObfuscationOptions.WithFormat(ErrorFormat.Xml);

        Assert.Equal(ErrorFormat.Xml, options.Format);
        Assert.Null(options.Language);
        Assert.False(options.RandomDelay);
    }

    /// <summary>
    /// 指定语言的工厂方法只设置语言
    /// </summary>
    [Fact]
    public void WithLanguage_OnlySetsLanguage()
    {
        var options = ErrorObfuscationOptions.WithLanguage(ProgrammingLanguage.Rust);

        Assert.Equal(ProgrammingLanguage.Rust, options.Language);
        Assert.Null(options.Format);
    }

    /// <summary>
    /// 同时指定语言与格式
    /// </summary>
    [Fact]
    public void With_SetsBothLanguageAndFormat()
    {
        var options = ErrorObfuscationOptions.With(ProgrammingLanguage.Go, ErrorFormat.Html);

        Assert.Equal(ProgrammingLanguage.Go, options.Language);
        Assert.Equal(ErrorFormat.Html, options.Format);
    }
}
