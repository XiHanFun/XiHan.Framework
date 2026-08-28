// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security.ErrorObfuscation;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Options;

namespace XiHan.Framework.Utils.Tests.Security.ErrorObfuscation;

/// <summary>
/// 错误混淆的语言字段回归测试
/// </summary>
/// <remarks>
/// <para>
/// 修复了两处：其一，按选项生成时若 <c>Format</c> 为 null 会直接改走全随机重载，
/// 把已经按 <c>Language</c> 取到的 error 整个丢弃，于是 <c>WithLanguage(x)</c> 单独使用完全不生效；
/// 其二，<c>ConvertToJsonDictionary</c> 逐字段拷贝时漏了 <c>Language</c>，
/// JSON 对象成了唯一不带语言的输出格式，混淆信息在各格式之间不自洽。
/// </para>
/// <para>
/// 混淆内容本身是随机的，能断言的是「语言这一维必须被贯彻」：指定语言后，
/// 无论落到哪种格式，写进去的都必须是同一个语言名。
/// </para>
/// </remarks>
public class ErrorObfuscationLanguageTests
{
    /// <summary>
    /// JSON 对象格式必须带上 language 键，且与指定语言一致
    /// </summary>
    [Theory]
    [InlineData(ProgrammingLanguage.CSharp)]
    [InlineData(ProgrammingLanguage.Java)]
    [InlineData(ProgrammingLanguage.Php)]
    [InlineData(ProgrammingLanguage.Go)]
    [InlineData(ProgrammingLanguage.Python)]
    [InlineData(ProgrammingLanguage.NodeJs)]
    [InlineData(ProgrammingLanguage.Ruby)]
    [InlineData(ProgrammingLanguage.Rust)]
    public void GenerateObfuscatedError_WithJsonObjectFormat_CarriesLanguageKey(ProgrammingLanguage language)
    {
        var error = Assert.IsType<Dictionary<string, object>>(
            ErrorObfuscationHelper.GenerateObfuscatedError(language, ErrorFormat.JsonObject));

        Assert.True(error.ContainsKey("language"), "JSON 对象格式缺少 language 键");
        Assert.Equal(language.ToString(), Assert.IsType<string>(error["language"]));
    }

    /// <summary>
    /// 只指定语言的重载同样把语言写进字典
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithLanguageOverload_CarriesLanguageKey()
    {
        var error = ErrorObfuscationHelper.GenerateObfuscatedError(ProgrammingLanguage.Ruby);

        Assert.Equal("Ruby", Assert.IsType<string>(error["language"]));
    }

    /// <summary>
    /// 随机生成的 JSON 对象也带 language 键，且是一个合法的语言名
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithRandomJsonObject_CarriesParsableLanguage()
    {
        for (var i = 0; i < 20; i++)
        {
            var error = Assert.IsType<Dictionary<string, object>>(
                ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonObject));

            var language = Assert.IsType<string>(error["language"]);
            Assert.True(Enum.TryParse<ProgrammingLanguage>(language, out _), $"language 不是合法语言名：{language}");
        }
    }

    /// <summary>
    /// 按选项生成且只指定语言时，随机挑中的格式里写的必须还是该语言
    /// </summary>
    /// <remarks>
    /// 修复前这条路径直接丢弃按语言取到的 error 改走全随机，语言会随机漂移到别的八分之一。
    /// 这里循环取样把五种格式都覆盖到，并对能观察到语言的格式逐一核对。
    /// </remarks>
    [Theory]
    [InlineData(ProgrammingLanguage.CSharp)]
    [InlineData(ProgrammingLanguage.Rust)]
    [InlineData(ProgrammingLanguage.Python)]
    public void GenerateObfuscatedError_WithLanguageOnlyOptions_HonorsLanguageInEveryFormat(ProgrammingLanguage language)
    {
        var options = ErrorObfuscationOptions.WithLanguage(language);
        var expected = language.ToString();
        var sawJsonObject = false;

        for (var i = 0; i < 200; i++)
        {
            var error = ErrorObfuscationHelper.GenerateObfuscatedError(options);

            if (error is Dictionary<string, object> dictionary && dictionary.TryGetValue("language", out var value))
            {
                Assert.Equal(expected, Assert.IsType<string>(value));
                sawJsonObject = true;
                continue;
            }

            if (error is not string text)
            {
                continue;
            }

            if (text.Contains("<Language>", StringComparison.Ordinal))
            {
                Assert.Contains($"<Language>{expected}</Language>", text);
            }
            else if (text.Contains("ERROR REPORT", StringComparison.Ordinal))
            {
                Assert.Contains($"Language:    {expected}", text);
            }
            else
            {
                Assert.Contains(expected, text);
            }
        }

        Assert.True(sawJsonObject, "200 次随机格式里一次都没抽到 JSON 对象格式，随机源可能异常。");
    }

    /// <summary>
    /// 只指定语言时格式仍然是随机的，不会退化成恒定一种
    /// </summary>
    /// <remarks>
    /// 修复是「沿用 error、格式照旧随机」，不是「固定成某种格式」，这里守住后半句。
    /// </remarks>
    [Fact]
    public void GenerateObfuscatedError_WithLanguageOnlyOptions_StillRandomizesFormat()
    {
        var options = ErrorObfuscationOptions.WithLanguage(ProgrammingLanguage.Go);
        var shapes = new HashSet<string>();

        for (var i = 0; i < 200; i++)
        {
            var error = ErrorObfuscationHelper.GenerateObfuscatedError(options);
            shapes.Add(error is Dictionary<string, object> dictionary
                ? dictionary.ContainsKey("errors") ? "jsonArray" : "jsonObject"
                : "string");
        }

        Assert.True(shapes.Count >= 3, $"200 次取样只见到 {shapes.Count} 种载体形状，格式没有随机化。");
    }

    /// <summary>
    /// 同时指定语言与格式时两者都生效（回归修复不能破坏原有分支）
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithLanguageAndFormatOptions_HonorsBoth()
    {
        var options = ErrorObfuscationOptions.With(ProgrammingLanguage.Java, ErrorFormat.JsonObject);

        var error = Assert.IsType<Dictionary<string, object>>(ErrorObfuscationHelper.GenerateObfuscatedError(options));

        Assert.Equal("Java", Assert.IsType<string>(error["language"]));
    }
}
