// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Prompts;

namespace XiHan.Framework.AI.Abstractions.Tests.Prompts;

/// <summary>
/// 回放 <c>prompt-templates.json</c>：提示词模板的渲染
/// </summary>
/// <remarks>
/// 规范 RFC-0022「聊天」的提示词（ADR-0277）：占位符 <c>{name}</c> 按名字取参数的值写进去，<c>{{</c> 与 <c>}}</c> 转义成字面量的花括号；参数的值原样写出、不再当模板解析，多给的参数不用。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AIPromptTemplateVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly PromptVectors _vectors = ConformanceVectors.Load<PromptVectors>("prompt-templates.json");

    [Fact]
    public void Render_GivesTheTextOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var arguments = vector.Arguments.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value, StringComparer.Ordinal);
            var rendered = new AIPromptTemplate("vectors", vector.Template).Render(arguments);
            if (!rendered.TryGetValue(out var text))
            {
                failures.Add($"{vector.Name}: rendering failed with '{rendered.Error}', the vector has '{vector.Text}'");
            }
            else if (text != vector.Text)
            {
                failures.Add($"{vector.Name}: rendered '{text}', the vector has '{vector.Text}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheEscapesAndTheArguments()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：没有占位符、转义的花括号、同一个占位符出现两次
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Count == 0 && vector.Text == vector.Template);
        Assert.Contains(_vectors.Cases, static vector => vector.Template.Contains("{{", StringComparison.Ordinal) && vector.Template.Contains("}}", StringComparison.Ordinal));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Keys.Any(name => vector.Template.Split("{" + name + "}").Length > 2));

        // 值里带花括号的原样写出、多给的参数不用、值为空串
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Values.Any(value => value.Contains('{') && vector.Text.Contains(value, StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Keys.Any(name => !vector.Template.Contains("{" + name + "}", StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Values.Any(static value => value.Length == 0));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Cases">用例</param>
    private sealed record PromptVectors(PromptCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="Arguments">名字到值的参数</param>
    /// <param name="Text">渲染出的文本</param>
    private sealed record PromptCase(string Name, string Template, Dictionary<string, string> Arguments, string Text);
}
