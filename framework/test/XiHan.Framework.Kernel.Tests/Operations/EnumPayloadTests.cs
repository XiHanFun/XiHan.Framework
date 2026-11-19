// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 载荷里的枚举按它自己的转换器判定
/// </summary>
/// <remarks>
/// 缺省的枚举转换器只读整数，字符串枚举转换器读成员名；解码前的形状检查不能一律按整数判（ADR-0247）
/// </remarks>
public sealed class EnumPayloadTests
{
    /// <summary>
    /// 枚举一律按字符串读写的描述
    /// </summary>
    private static readonly OperationDescriptor _stringEnums = new OperationDescriptor<ShapeOperation, ShapeRequest, ShapeRequest>(ShapeOperation.Contract, "shapes", StringEnumJsonContext.Default.ShapeRequest, StringEnumJsonContext.Default.ShapeRequest);

    /// <summary>
    /// 枚举按缺省转换器读整数的描述
    /// </summary>
    private static readonly OperationDescriptor _integerEnums = new OperationDescriptor<ShapeOperation, ShapeRequest, ShapeRequest>(ShapeOperation.Contract, "shapes", IntegerEnumJsonContext.Default.ShapeRequest, IntegerEnumJsonContext.Default.ShapeRequest);

    [Fact]
    public void StringEnums_DecodeMemberNames()
    {
        var request = Decode(_stringEnums, """{"level":"High","mode":"On","levels":["Low","High"],"count":1}""");

        Assert.Equal(ShapeLevel.High, request.Level);
        Assert.Equal(ShapeMode.On, request.Mode);
        Assert.Equal([ShapeLevel.Low, ShapeLevel.High], request.Levels);
        Assert.Equal(1, request.Count);
    }

    [Fact]
    public void StringEnums_StillDecodeIntegers()
    {
        var request = Decode(_stringEnums, """{"level":2,"mode":1}""");

        Assert.Equal(ShapeLevel.High, request.Level);
        Assert.Equal(ShapeMode.On, request.Mode);
    }

    [Theory]
    [InlineData("""{"level":"Medium"}""", "$.level")]
    [InlineData("""{"level":true}""", "$.level")]
    [InlineData("""{"level":256}""", "$.level")]
    [InlineData("""{"level":1.5}""", "$.level")]
    [InlineData("""{"levels":["Low","Loud"]}""", "$.levels[1]")]
    [InlineData("""{"level":"Medium","count":"1"}""", "$.level")]
    [InlineData("""{"count":"1","level":"Medium"}""", "$.count")]
    public void StringEnums_ReportTheFirstProblemInTextOrder(string payload, string reason)
        => Assert.Equal(reason, Reason(_stringEnums, payload));

    [Fact]
    public void TypeLevelConverter_DecodesMemberNames_UnderAnIntegerContext()
    {
        var request = Decode(_integerEnums, """{"level":2,"mode":"On"}""");

        Assert.Equal(ShapeLevel.High, request.Level);
        Assert.Equal(ShapeMode.On, request.Mode);
    }

    [Theory]
    [InlineData("""{"level":"High"}""", "$.level")]
    [InlineData("""{"level":256}""", "$.level")]
    [InlineData("""{"mode":"Dim"}""", "$.mode")]
    [InlineData("""{"level":"High","count":"1"}""", "$.level")]
    [InlineData("""{"count":"1","level":"High"}""", "$.count")]
    [InlineData("""{"levels":[1,"High"]}""", "$.levels[1]")]
    public void IntegerEnums_RejectMemberNames_InTextOrder(string payload, string reason)
        => Assert.Equal(reason, Reason(_integerEnums, payload));

    /// <summary>
    /// 解码一段必须合法的载荷
    /// </summary>
    private static ShapeRequest Decode(OperationDescriptor descriptor, string payload)
    {
        var result = descriptor.DecodeRequest(Encoding.UTF8.GetBytes(payload));
        Assert.True(result.TryGetValue(out var request), result.Error?.ToString());
        return Assert.IsType<ShapeRequest>(request);
    }

    /// <summary>
    /// 解码一段不合法的载荷，取错误细节里的路径
    /// </summary>
    private static string? Reason(OperationDescriptor descriptor, string payload)
    {
        var result = descriptor.DecodeRequest(Encoding.UTF8.GetBytes(payload));
        Assert.False(result.IsSuccess);
        Assert.Equal(KernelErrors.InvalidOperationPayload, result.Error!.Code);
        return result.Error.Details!.TryGetValue("reason", out var value) ? value as string : null;
    }
}
