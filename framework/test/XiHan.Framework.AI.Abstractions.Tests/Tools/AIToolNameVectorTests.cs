// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.AI.Abstractions.Tests.Tools;

/// <summary>
/// 回放 <c>tool-names.json</c>：操作作为 AI 工具时的名字与描述键
/// </summary>
/// <remarks>
/// 规范 RFC-0022「工具」（ADR-0277）：缺省的工具名是 <c>&lt;resource&gt;_&lt;verb&gt;</c>，<c>-</c> 与 <c>.</c> 换成 <c>_</c>；超过上限时截成前 55 个字符加 <c>_</c> 加全名 SHA-256 的前 8 位小写十六进制。描述键是操作 ID 里的 <c>-</c> 换成 <c>_</c> 再加 <c>.description</c>。向量是规范的一部分，这里只回放，不另存一份
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

            // 推出的名字必须是模型 API 收得下的工具名
            Assert.True(AIIdentifiers.IsValidToolName(tool), tool);
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
    /// <param name="Cases">用例</param>
    private sealed record ToolNameVectors(int MaxLength, ToolNameCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Operation">操作 ID</param>
    /// <param name="Tool">推出的工具名</param>
    /// <param name="DescriptionKey">工具描述在消息目录里的键</param>
    private sealed record ToolNameCase(string Name, string Operation, string Tool, string DescriptionKey);
}
