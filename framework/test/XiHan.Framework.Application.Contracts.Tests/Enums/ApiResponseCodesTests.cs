// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Enums;

namespace XiHan.Framework.Application.Contracts.Tests.Enums;

/// <summary>
/// 统一返回码枚举测试
/// </summary>
/// <remarks>
/// 该枚举会被序列化进接口响应体、写入日志与前端判断分支，属于对外协议的一部分，
/// 因此这里锁死每个成员的数值、描述特性的存在性，以及「强制以 int 形态出入 JSON」的转换器契约。
/// 数值一旦漂移，已发布的客户端会静默走错分支，所以宁可让本组用例在改动时显式失败。
/// </remarks>
public class ApiResponseCodesTests
{
    /// <summary>
    /// 每个成员的数值必须与协议约定一致，不允许漂移
    /// </summary>
    [Theory]
    [InlineData(ApiResponseCodes.Continue, 100)]
    [InlineData(ApiResponseCodes.SwitchingProtocols, 101)]
    [InlineData(ApiResponseCodes.Success, 200)]
    [InlineData(ApiResponseCodes.Created, 201)]
    [InlineData(ApiResponseCodes.Accepted, 202)]
    [InlineData(ApiResponseCodes.NoContent, 204)]
    [InlineData(ApiResponseCodes.MultipleChoices, 300)]
    [InlineData(ApiResponseCodes.MovedPermanently, 301)]
    [InlineData(ApiResponseCodes.Found, 302)]
    [InlineData(ApiResponseCodes.NotModified, 304)]
    [InlineData(ApiResponseCodes.BadRequest, 400)]
    [InlineData(ApiResponseCodes.Unauthorized, 401)]
    [InlineData(ApiResponseCodes.Forbidden, 403)]
    [InlineData(ApiResponseCodes.NotFound, 404)]
    [InlineData(ApiResponseCodes.MethodNotAllowed, 405)]
    [InlineData(ApiResponseCodes.RequestTimeout, 408)]
    [InlineData(ApiResponseCodes.Conflict, 409)]
    [InlineData(ApiResponseCodes.Gone, 410)]
    [InlineData(ApiResponseCodes.UnsupportedMediaType, 415)]
    [InlineData(ApiResponseCodes.UnprocessableEntity, 422)]
    [InlineData(ApiResponseCodes.Locked, 423)]
    [InlineData(ApiResponseCodes.TooManyRequests, 429)]
    [InlineData(ApiResponseCodes.InternalServerError, 500)]
    [InlineData(ApiResponseCodes.NotImplemented, 501)]
    [InlineData(ApiResponseCodes.BadGateway, 502)]
    [InlineData(ApiResponseCodes.ServiceUnavailable, 503)]
    [InlineData(ApiResponseCodes.GatewayTimeout, 504)]
    [InlineData(ApiResponseCodes.LoginExpired, 10001)]
    [InlineData(ApiResponseCodes.TokenInvalid, 10002)]
    [InlineData(ApiResponseCodes.TokenExpired, 10003)]
    [InlineData(ApiResponseCodes.PermissionDenied, 10004)]
    [InlineData(ApiResponseCodes.ValidationFailed, 11000)]
    [InlineData(ApiResponseCodes.BusinessFailed, 12000)]
    [InlineData(ApiResponseCodes.DatabaseError, 13000)]
    [InlineData(ApiResponseCodes.ThirdPartyServiceError, 14000)]
    public void NumericValue_ForEachMember_IsStable(ApiResponseCodes code, int expected)
    {
        Assert.Equal(expected, (int)code);
    }

    /// <summary>
    /// 底层类型必须是 int，业务码上限 99999 才装得下且与前端 number 语义一致
    /// </summary>
    [Fact]
    public void UnderlyingType_IsInt32()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ApiResponseCodes)));
    }

    /// <summary>
    /// 成员数量与本测试锁定的清单一致，新增成员必须同步补测
    /// </summary>
    [Fact]
    public void MemberCount_MatchesLockedContract()
    {
        Assert.Equal(35, Enum.GetNames<ApiResponseCodes>().Length);
    }

    /// <summary>
    /// 不允许出现数值重复的成员（重复会让 Enum.GetName 返回不确定的名字）
    /// </summary>
    [Fact]
    public void NumericValues_AreUniqueAcrossMembers()
    {
        var names = Enum.GetNames<ApiResponseCodes>();
        var distinctValues = Enum.GetValues<ApiResponseCodes>().Distinct().Count();

        Assert.Equal(names.Length, distinctValues);
    }

    /// <summary>
    /// 每个成员都必须带 Description 特性
    /// </summary>
    /// <remarks>
    /// ApiResponse 的 Message 默认取自该特性，缺失时会退化成回显英文成员名，属于对外可见的劣化。
    /// </remarks>
    [Fact]
    public void EveryMember_HasDescriptionAttribute()
    {
        var missing = Enum.GetNames<ApiResponseCodes>()
            .Where(name => typeof(ApiResponseCodes).GetField(name)?.GetCustomAttribute<DescriptionAttribute>() is null)
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    /// 关键返回码的描述文案锁定
    /// </summary>
    [Theory]
    [InlineData(ApiResponseCodes.Success, "请求成功")]
    [InlineData(ApiResponseCodes.Created, "资源创建成功")]
    [InlineData(ApiResponseCodes.BadRequest, "请求错误")]
    [InlineData(ApiResponseCodes.Unauthorized, "未授权")]
    [InlineData(ApiResponseCodes.Forbidden, "禁止访问")]
    [InlineData(ApiResponseCodes.NotFound, "资源不存在")]
    [InlineData(ApiResponseCodes.Locked, "会话已锁定")]
    [InlineData(ApiResponseCodes.UnprocessableEntity, "请求语义错误")]
    [InlineData(ApiResponseCodes.TooManyRequests, "请求过于频繁")]
    [InlineData(ApiResponseCodes.InternalServerError, "服务器内部错误")]
    [InlineData(ApiResponseCodes.ServiceUnavailable, "服务不可用")]
    [InlineData(ApiResponseCodes.LoginExpired, "登录已过期")]
    [InlineData(ApiResponseCodes.PermissionDenied, "权限不足")]
    [InlineData(ApiResponseCodes.ValidationFailed, "数据校验失败")]
    [InlineData(ApiResponseCodes.BusinessFailed, "业务处理失败")]
    public void Description_ForKeyCodes_IsStable(ApiResponseCodes code, string expected)
    {
        var field = typeof(ApiResponseCodes).GetField(code.ToString());

        Assert.NotNull(field);
        Assert.Equal(expected, field!.GetCustomAttribute<DescriptionAttribute>()?.Description);
    }

    /// <summary>
    /// 类型级 NumericEnumConverter 让枚举以数字形态写入 JSON
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_WritesNumericValue()
    {
        Assert.Equal("10004", JsonSerializer.Serialize(ApiResponseCodes.PermissionDenied));
        Assert.Equal("200", JsonSerializer.Serialize(ApiResponseCodes.Success));
    }

    /// <summary>
    /// 反序列化同时兼容数字、数字字符串与成员名（大小写不敏感）
    /// </summary>
    /// <remarks>
    /// 兼容字符串是为了容忍历史客户端与手写 JSON；这三种来源都必须落到同一个枚举值。
    /// </remarks>
    [Theory]
    [InlineData("200")]
    [InlineData("\"200\"")]
    [InlineData("\"Success\"")]
    [InlineData("\"success\"")]
    public void Deserialize_AcceptsNumberAndName(string json)
    {
        Assert.Equal(ApiResponseCodes.Success, JsonSerializer.Deserialize<ApiResponseCodes>(json));
    }

    /// <summary>
    /// 未定义的数值可以透传，避免旧客户端解析新返回码时直接抛错
    /// </summary>
    [Fact]
    public void Deserialize_UndefinedNumber_PassesThrough()
    {
        var code = JsonSerializer.Deserialize<ApiResponseCodes>("99999");

        Assert.Equal(99999, (int)code);
        Assert.False(Enum.IsDefined(code));
    }

    /// <summary>
    /// 既不是数字也不是字符串的 JSON 记号必须抛 JsonException
    /// </summary>
    [Fact]
    public void Deserialize_WhenTokenIsBoolean_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
        {
            _ = JsonSerializer.Deserialize<ApiResponseCodes>("true");
        });
    }

    /// <summary>
    /// 无法识别的字符串同样抛 JsonException，而不是静默落到默认值 0
    /// </summary>
    [Fact]
    public void Deserialize_WhenNameUnknown_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
        {
            _ = JsonSerializer.Deserialize<ApiResponseCodes>("\"NotAnApiResponseCode\"");
        });
    }
}
