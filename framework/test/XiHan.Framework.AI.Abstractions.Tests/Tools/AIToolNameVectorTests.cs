// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.AI.Abstractions.Tests.Tools;

/// <summary>
/// 回放 <c>tool-names.json</c>：操作作为 AI 工具时的名字与描述键
/// </summary>
/// <remarks>
/// 规范 RFC-0022「工具」（ADR-0277、ADR-0286）：工具名的文法是 <c>^[A-Za-z0-9_-]{1,64}$</c>；缺省的工具名是 <c>&lt;resource&gt;_&lt;verb&gt;</c>，<c>-</c> 与 <c>.</c> 换成 <c>_</c>，超过上限时取前 55 个字符、去掉结尾的 <c>_</c>，再接 <c>_</c> 与全名 SHA-256 的前 8 位小写十六进制。描述键是操作 ID 里的 <c>-</c> 换成 <c>_</c> 再加 <c>.description</c>。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AIToolNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ToolNameVectors _vectors = ConformanceVectors.Load<ToolNameVectors>("tool-names.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(AIToolNameConvention.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValidToolName_FollowsTheGrammarSamples()
    {
        Assert.NotEmpty(_vectors.Valid);
        Assert.NotEmpty(_vectors.Invalid);
        Assert.All(_vectors.Valid, static name => Assert.True(AIIdentifiers.IsValidToolName(name), name));
        Assert.All(_vectors.Invalid, static name => Assert.False(AIIdentifiers.IsValidToolName(name), name));

        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只多一个字符的要拒，结尾带换行的要拒
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && name.All(char.IsAsciiLetterOrDigit));
        Assert.Contains(_vectors.Invalid, static name => name.EndsWith('\n'));
    }

    [Fact]
    public void Derive_GivesTheToolNameOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var tool = AIToolNameConvention.Derive(new OperationContract(vector.Operation));
            if (tool != vector.Tool)
            {
                failures.Add($"{vector.Name}: derived '{tool}', the vector has '{vector.Tool}'");
            }

            // 推出的名字必须是模型 API 收得下的工具名，也不出现连着的两个下划线
            Assert.True(AIIdentifiers.IsValidToolName(tool), tool);
            Assert.DoesNotContain("__", tool, StringComparison.Ordinal);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void DescriptionKeyOf_GivesTheMessageKeyOfEveryCase()
        => Assert.All(_vectors.Cases, static vector => Assert.Equal(vector.DescriptionKey, AIToolNameConvention.DescriptionKeyOf(vector.Operation)));

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后截断一条都没核对还照样通过：没截之前正好到上限的不截，多一个字符的就截，截出来的名字正好到上限
        Assert.Contains(_vectors.Cases, vector => Untruncated(vector.Operation).Length == _vectors.MaxLength && vector.Tool == Untruncated(vector.Operation));
        Assert.Contains(_vectors.Cases, vector => Untruncated(vector.Operation).Length == _vectors.MaxLength + 1 && vector.Tool.Length == _vectors.MaxLength && vector.Tool != Untruncated(vector.Operation));

        // 前 55 个字符正好以下划线结尾的：去掉它，名字比上限短一个字符；下划线在第 54、第 56 个字符上的不受影响
        Assert.Contains(_vectors.Cases, vector => Untruncated(vector.Operation) is { Length: > 64 } name && name[54] == '_' && vector.Tool.Length == _vectors.MaxLength - 1);
        Assert.Contains(_vectors.Cases, vector => Untruncated(vector.Operation) is { Length: > 64 } name && name[53] == '_' && vector.Tool.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Cases, vector => Untruncated(vector.Operation) is { Length: > 64 } name && name[55] == '_' && vector.Tool.Length == _vectors.MaxLength);

        // 连字符在工具名与描述键里都换成下划线，描述键保留点
        Assert.Contains(_vectors.Cases, static vector => vector.Operation.Contains('-') && !vector.Tool.Contains('-') && !vector.DescriptionKey.Contains('-'));
    }

    /// <summary>
    /// 没有截之前的工具名
    /// </summary>
    /// <remarks>
    /// 操作 ID 恰好一个点，把点与连字符都换成下划线就是 <c>&lt;resource&gt;_&lt;verb&gt;</c>
    /// </remarks>
    private static string Untruncated(string operation)
        => operation.Replace('-', '_').Replace('.', '_');

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">工具名最长的字符数</param>
    /// <param name="Valid">合文法的工具名</param>
    /// <param name="Invalid">不合文法的工具名</param>
    /// <param name="Cases">由操作 ID 推出工具名的用例</param>
    private sealed record ToolNameVectors(int MaxLength, string[] Valid, string[] Invalid, ToolNameCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Operation">操作 ID</param>
    /// <param name="Tool">推出的工具名</param>
    /// <param name="DescriptionKey">工具描述在消息目录里的键</param>
    private sealed record ToolNameCase(string Name, string Operation, string Tool, string DescriptionKey);
}
