// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 逐个码位的 Unicode 简单大小写映射
/// </summary>
/// <remarks>
/// 关键字的折叠与表达式的 <c>upper</c> / <c>lower</c> 共用（RFC-0021「关键字的小写」与「内置函数」，ADR-0296、ADR-0303）：整串交给不变文化成批转换之后补上 U+0130、U+0131 与平面以外的字母，结果必须与逐个码位取映射相同
/// </remarks>
public sealed class WorkflowCaseMappingTests
{
    [Fact]
    public void EveryCodePoint_MapsToOneCodePointOfTheSameLength()
    {
        // 简单映射一个码位对一个码位、不跨平面：数据库的查询列与表达式的长度都按码元算，转换前后不能变长变短
        var failures = new List<string>();
        for (var value = 0; value <= 0x10FFFF; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            var text = new Rune(value).ToString();
            var lower = WorkflowCaseMapping.ToLower(text);
            var upper = WorkflowCaseMapping.ToUpper(text);
            if (lower.Length != text.Length || upper.Length != text.Length || Rune.GetRuneAt(lower, 0).Utf16SequenceLength != text.Length || Rune.GetRuneAt(upper, 0).Utf16SequenceLength != text.Length)
            {
                failures.Add($"U+{value:X4}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Batches_MapLikeEveryCodePointOnItsOwn()
    {
        // 整串转换走成批的路径，单个码位也走同一条路径：每 512 个码位拼成一段，结果必须是各码位各自转换后接起来
        var failures = new List<string>();
        var batch = new StringBuilder();
        var expectedLower = new StringBuilder();
        var expectedUpper = new StringBuilder();
        for (var value = 0; value <= 0x10FFFF; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            var text = new Rune(value).ToString();
            batch.Append(text);
            expectedLower.Append(WorkflowCaseMapping.ToLower(text));
            expectedUpper.Append(WorkflowCaseMapping.ToUpper(text));
            if (batch.Length >= 512 || value == 0x10FFFF)
            {
                if (WorkflowCaseMapping.ToLower(batch.ToString()) != expectedLower.ToString() || WorkflowCaseMapping.ToUpper(batch.ToString()) != expectedUpper.ToString())
                {
                    failures.Add($"the batch ending at U+{value:X4}");
                }

                batch.Clear();
                expectedLower.Clear();
                expectedUpper.Clear();
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void TheDottedIs_FollowTheSimpleMapping_NotTheInvariantCulture()
    {
        // 不变文化不转换这两个字符：简单映射里 İ 的小写是 i，ı 的大写是 I；它们本身的另一种大小写不变
        Assert.Equal("i", WorkflowCaseMapping.ToLower("İ"));
        Assert.Equal("İ", WorkflowCaseMapping.ToUpper("İ"));
        Assert.Equal("I", WorkflowCaseMapping.ToUpper("ı"));
        Assert.Equal("ı", WorkflowCaseMapping.ToLower("ı"));
        Assert.Equal("İI", WorkflowCaseMapping.ToUpper("İi"));
        Assert.Equal("aıbic", WorkflowCaseMapping.ToLower("AıBİC"));
        Assert.Equal("AIBİC", WorkflowCaseMapping.ToUpper("aıbİc"));
    }

    [Fact]
    public void SupplementaryLetters_MapAsWholeCodePoints()
    {
        Assert.Equal("𐐀𐐁", WorkflowCaseMapping.ToUpper("𐐨𐐩"));
        Assert.Equal("a𐐨b", WorkflowCaseMapping.ToLower("A𐐀B"));
        Assert.Equal("😀", WorkflowCaseMapping.ToUpper("😀"));
    }

    [Fact]
    public void SimpleMappings_DoNotExpand()
    {
        // 完全映射才会把 ß 写成 SS、把 ﬀ 写成 FF；简单映射里它们没有大写，不变
        Assert.Equal("AßC", WorkflowCaseMapping.ToUpper("aßc"));
        Assert.Equal("ŉﬀ", WorkflowCaseMapping.ToUpper("ŉﬀ"));
        Assert.Equal("ß", WorkflowCaseMapping.ToLower("ẞ"));
        Assert.Equal("S", WorkflowCaseMapping.ToUpper("ſ"));
        Assert.Equal("ǄǄ", WorkflowCaseMapping.ToUpper("ǅǆ"));
        Assert.Equal("ǆǆ", WorkflowCaseMapping.ToLower("ǅǄ"));
        Assert.Equal("ασ", WorkflowCaseMapping.ToLower("ΑΣ"));
    }

    [Fact]
    public void LoneSurrogates_StayAsTheyAre()
    {
        Assert.Equal("\uD801a", WorkflowCaseMapping.ToLower("\uD801A"));
        Assert.Equal("A\uDC00", WorkflowCaseMapping.ToUpper("a\uDC00"));
        Assert.Equal("\uDC00\uD801I", WorkflowCaseMapping.ToUpper("\uDC00\uD801ı"));
    }

    [Fact]
    public void KeywordFolding_IsTheLowercaseMapping_WithTheSeparatorAsASpace()
        => Assert.Equal(WorkflowCaseMapping.ToLower("İ𐐀") + " a", WorkflowKeyword.Fold("İ𐐀\u001FA"));
}
