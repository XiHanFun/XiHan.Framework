// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// 反序列化容错开关与 Try 语义测试
/// </summary>
/// <remarks>
/// 覆盖两处"开关不生效"的修复：
/// 一是 ValidateJson 的预校验原来走默认 JsonDocumentOptions，把 AllowTrailingCommas / ReadCommentHandling
/// 变成死开关（Lenient 预设允许的写法在预校验就被拒）；
/// 二是 TryDeserialize 原来透传调用方的 ErrorHandling，UseDefault/Ignore/Log 会把失败吞成"成功且结果为 null"。
/// </remarks>
public class JsonHelperToleranceSwitchTests
{
    /// <summary>
    /// 宽松预设下尾随逗号可被接受
    /// </summary>
    [Fact]
    public void Deserialize_WithLenientOptions_AcceptsTrailingComma()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{ \"name\": \"曦寒\", \"age\": 18, }", JsonDeserializeOptions.Lenient);

        Assert.Equal("曦寒", user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 宽松预设下注释可被跳过
    /// </summary>
    [Fact]
    public void Deserialize_WithLenientOptions_AcceptsComment()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{ /* 注释 */ \"name\": \"曦寒\" }", JsonDeserializeOptions.Lenient);

        Assert.Equal("曦寒", user.Name);
    }

    /// <summary>
    /// 默认选项本身就打开了两个容错开关，开启校验时也应放行
    /// </summary>
    /// <remarks>
    /// 默认 JsonDeserializeOptions 的 AllowTrailingCommas 与 ReadCommentHandling 都是 true、
    /// ValidateJson 也是 true，修复前这三者叠加的结果是容错开关永远走不到解析器。
    /// </remarks>
    [Fact]
    public void Deserialize_WithDefaultOptions_AcceptsTrailingCommaAndComment()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{ \"name\": \"曦寒\", // 行注释\n \"age\": 18, }");

        Assert.Equal("曦寒", user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 严格预设下尾随逗号与注释仍被拒绝
    /// </summary>
    /// <param name="json">待反序列化的 JSON</param>
    [Theory]
    [InlineData("{\"name\":\"曦寒\",}")]
    [InlineData("{/* 注释 */\"name\":\"曦寒\"}")]
    public void Deserialize_WithStrictOptions_RejectsToleratedSyntax(string json)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>(json, JsonDeserializeOptions.Strict);
        });
    }

    /// <summary>
    /// 关闭容错开关但保留校验时，尾随逗号被预校验拦下
    /// </summary>
    [Fact]
    public void Deserialize_WhenTrailingCommaDisabled_RejectsTrailingComma()
    {
        var options = new JsonDeserializeOptions { AllowTrailingCommas = false };

        Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>("{\"name\":\"曦寒\",}", options);
        });
    }

    /// <summary>
    /// 无论错误处理策略是否吞异常，TryDeserialize 遇到非法 JSON 都返回 false
    /// </summary>
    /// <param name="handling">错误处理策略</param>
    [Theory]
    [InlineData(JsonErrorHandling.ThrowException)]
    [InlineData(JsonErrorHandling.Ignore)]
    [InlineData(JsonErrorHandling.UseDefault)]
    [InlineData(JsonErrorHandling.Log)]
    public void TryDeserialize_WhenJsonInvalid_ReturnsFalseForEveryErrorHandling(JsonErrorHandling handling)
    {
        var options = new JsonDeserializeOptions { ErrorHandling = handling };

        var succeeded = JsonHelper.TryDeserialize<JsonSampleUser>("{不是 JSON", out var user, options);

        Assert.False(succeeded);
        Assert.Null(user);
    }

    /// <summary>
    /// TryDeserialize 不会就地改写调用方持有的选项实例
    /// </summary>
    [Fact]
    public void TryDeserialize_DoesNotMutateCallerOptions()
    {
        var options = new JsonDeserializeOptions { ErrorHandling = JsonErrorHandling.UseDefault };

        JsonHelper.TryDeserialize<JsonSampleUser>("{不是 JSON", out _, options);

        Assert.Equal(JsonErrorHandling.UseDefault, options.ErrorHandling);
    }

    /// <summary>
    /// 吞异常策略下合法 JSON 仍然返回 true 并给出结果
    /// </summary>
    [Fact]
    public void TryDeserialize_WhenJsonValid_ReturnsTrueEvenWithSwallowingErrorHandling()
    {
        var options = new JsonDeserializeOptions { ErrorHandling = JsonErrorHandling.UseDefault };

        var succeeded = JsonHelper.TryDeserialize<JsonSampleUser>("{\"name\":\"曦寒\",\"age\":18}", out var user, options);

        Assert.True(succeeded);
        Assert.NotNull(user);
        Assert.Equal("曦寒", user!.Name);
    }

    /// <summary>
    /// TryDeserialize 仍然沿用调用方选项里的容错开关
    /// </summary>
    /// <remarks>
    /// 修复用的是"选项副本 + 强制抛异常"，副本必须逐字段复制，否则容错开关会在 Try 路径上凭空丢失。
    /// </remarks>
    [Fact]
    public void TryDeserialize_KeepsToleranceSwitchesFromOptions()
    {
        Assert.True(JsonHelper.TryDeserialize<JsonSampleUser>("{\"name\":\"曦寒\",}", out var lenient, JsonDeserializeOptions.Lenient));
        Assert.Equal("曦寒", lenient!.Name);

        Assert.False(JsonHelper.TryDeserialize<JsonSampleUser>("{\"name\":\"曦寒\",}", out var strict, JsonDeserializeOptions.Strict));
        Assert.Null(strict);
    }

    /// <summary>
    /// 结果恰好等于类型默认值时 TryDeserialize 仍然返回 true
    /// </summary>
    /// <remarks>
    /// 这条是反例守卫：如果改成"结果等于 default 就返回 false"，
    /// 合法的 0 / false 会被误判为失败，所以修复走的是"强制抛异常策略"而不是判定默认值。
    /// </remarks>
    [Fact]
    public void TryDeserialize_WhenResultEqualsTypeDefault_StillReturnsTrue()
    {
        Assert.True(JsonHelper.TryDeserialize<int>("0", out var number));
        Assert.Equal(0, number);

        Assert.True(JsonHelper.TryDeserialize<bool>("false", out var flag));
        Assert.False(flag);
    }
}
