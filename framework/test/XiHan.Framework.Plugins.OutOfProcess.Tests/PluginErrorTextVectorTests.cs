// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 回放 <c>error-text-writing.json</c>：写进插件协议的 <c>Error</c> 文案按最大子部分换成 U+FFFD
/// </summary>
/// <remarks>
/// 规范 <c>plugin/invocation.md</c> §3「写出 <c>Error</c> 的文案」（ADR-0339、ADR-0346）。向量在 SDK 认领的 <c>plugin</c> 档里，参考实现不认领这一档，但期望值由 .NET 算出、两种载体都经 <see cref="PluginErrorMapper.ToProto"/> 写出文案，所以照样回放：UTF-16 一组按 .NET 的输入形状经 <see cref="PluginErrorMapper.ToProto"/>；码点一组逐个码点经它（代理项码点以单个码元交给它）；字节一组是以字节存放字符串的语言的写法，.NET 的对照是 <see cref="Encoding.UTF8"/> 的解码
/// </remarks>
public sealed class PluginErrorTextVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ErrorTextVectors _vectors = SpecVectors.Read<ErrorTextVectors>("plugin", "error-text-writing.json");

    [Fact]
    public void ToProto_WritesTheUtf16Texts_AsTheVectorsExpect()
    {
        Assert.NotEmpty(_vectors.Utf16);

        // 内部错误的文案经同一处替换；不一致的用例按名字列出
        var mismatches = _vectors.Utf16
            .Where(static c =>
            {
                var text = SpecVectors.FromCodeUnits(c.Utf16);
                var proto = PluginErrorMapper.ToProto(Error.Literal("ORDERS_UNEXPECTED_001", text, null, Error.Literal("ORDERS_UNEXPECTED_002", text)));
                return proto.Text != c.Expected || proto.Inner.Text != c.Expected;
            })
            .Select(static c => c.Name)
            .ToList();

        Assert.Empty(mismatches);
        Assert.Contains(_vectors.Utf16, static c => UnicodeText.HasLoneSurrogate(SpecVectors.FromCodeUnits(c.Utf16)));
        Assert.All(_vectors.Utf16, static c => Assert.False(UnicodeText.HasLoneSurrogate(c.Expected), c.Name));
    }

    [Fact]
    public void ToProto_WritesEachCodePoint_AsTheVectorsExpect()
    {
        Assert.NotEmpty(_vectors.CodePoints);

        var mismatches = _vectors.CodePoints.Where(static c => Written(c.CodePoints) != c.Expected).Select(static c => c.Name).ToList();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Utf8Decoding_ReplacesTheMaximalSubparts_AsTheVectorsExpect()
    {
        Assert.NotEmpty(_vectors.Bytes);

        // 以字节存放字符串的语言的写法：.NET 的 Encoding.UTF8 解码就是按最大子部分换成 U+FFFD（ADR-0339 的实测）
        var mismatches = _vectors.Bytes.Where(static c => Encoding.UTF8.GetString(Convert.FromHexString(c.Hex)) != c.Expected).Select(static c => c.Name).ToList();

        Assert.Empty(mismatches);
    }

    /// <summary>
    /// 逐个码点写出：代理项码点以单个码元的字符串经 <see cref="PluginErrorMapper.ToProto"/> 换掉，其余码点原样
    /// </summary>
    private static string Written(IReadOnlyList<string> codePoints)
    {
        var builder = new StringBuilder();
        foreach (var point in codePoints)
        {
            var value = int.Parse(point, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            builder.Append(value is >= 0xD800 and <= 0xDFFF
                ? PluginErrorMapper.ToProto(Error.Literal("ORDERS_UNEXPECTED_001", ((char)value).ToString())).Text
                : char.ConvertFromUtf32(value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Utf16">以 UTF-16 存放的文案</param>
    /// <param name="CodePoints">以码点存放的文案（Python 的 <c>str</c>）</param>
    /// <param name="Bytes">以字节存放的文案</param>
    private sealed record ErrorTextVectors(Utf16Case[] Utf16, CodePointCase[] CodePoints, BytesCase[] Bytes);

    /// <summary>
    /// UTF-16 的一条用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Utf16">以四位十六进制写出的码元</param>
    /// <param name="Expected">替换之后写出的文案</param>
    private sealed record Utf16Case(string Name, string[] Utf16, string Expected);

    /// <summary>
    /// 码点的一条用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="CodePoints">以四到六位十六进制写出的码点</param>
    /// <param name="Expected">替换之后写出的文案</param>
    private sealed record CodePointCase(string Name, string[] CodePoints, string Expected);

    /// <summary>
    /// 字节的一条用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Hex">以十六进制写出的原始字节</param>
    /// <param name="Expected">替换之后写出的文案</param>
    private sealed record BytesCase(string Name, string Hex, string Expected);
}
