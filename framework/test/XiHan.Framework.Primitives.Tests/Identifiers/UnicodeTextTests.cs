// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Primitives.Tests.Identifiers;

/// <summary>
/// 孤立代理项的判定
/// </summary>
/// <remarks>
/// RFC-0001「孤立的代理项」（ADR-0321）：单独的高代理项、单独的低代理项、颠倒的一对、末尾的高代理项都算，成对的代理项不算。样例不放进 <c>[InlineData]</c>：特性的字符串实参按 UTF-8 存进元数据，孤立的代理项在那里就已经换成了 U+FFFD
/// </remarks>
public sealed class UnicodeTextTests
{
    /// <summary>
    /// 严格的 UTF-8 编码：编不成即抛出，用来与判定互相核对
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [Fact]
    public void LoneSurrogates_AreFound()
    {
        string[] texts = ["a\uD800b", "a\uDC00b", "\uDE00\uD83D", "a\uD800", "\uD83D😀", "\uDFFF"];
        Assert.All(texts, static text =>
        {
            Assert.True(UnicodeText.HasLoneSurrogate(text));
            Assert.Throws<EncoderFallbackException>(() => _strictUtf8.GetByteCount(text));
        });
    }

    [Fact]
    public void TextWithoutLoneSurrogates_IsNotFlagged()
    {
        string[] texts = [string.Empty, "a", "订单", "😀", "a😀b􏿿", "a�b"];
        Assert.All(texts, static text =>
        {
            Assert.False(UnicodeText.HasLoneSurrogate(text));
            Assert.Equal(Encoding.UTF8.GetByteCount(text), _strictUtf8.GetByteCount(text));
        });
    }

    [Fact]
    public void TwoTextsThatDifferOnlyInALoneSurrogate_EncodeToTheSameLenientBytes_SoBothAreFlagged()
    {
        // 宽松的 UTF-8 把两个不同的孤立代理项都换成 EF BF BD：编出来的字节分不开，所以文法上就拒收
        Assert.Equal(Encoding.UTF8.GetBytes("a\uD800"), Encoding.UTF8.GetBytes("a\uDBFF"));
        Assert.True(UnicodeText.HasLoneSurrogate("a\uD800"));
        Assert.True(UnicodeText.HasLoneSurrogate("a\uDBFF"));
    }
}
