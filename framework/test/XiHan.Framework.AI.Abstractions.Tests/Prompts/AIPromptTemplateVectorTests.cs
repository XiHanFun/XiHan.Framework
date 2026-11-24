// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Prompts;

/// <summary>
/// 回放 <c>prompt-templates.json</c>：提示词模板的渲染
/// </summary>
/// <remarks>
/// 规范 RFC-0022「聊天」的提示词（ADR-0277、ADR-0286）：占位符 <c>{name}</c> 按名字取参数的值写进去，名字两侧的空白去掉，<c>{{</c> 与 <c>}}</c> 转义成字面量的花括号，没有闭合的 <c>{</c> 连同其后的文本原样写出；参数的值原样写出、不再当模板解析，<see langword="null"/> 写成空串，多给的参数不用；缺少参数时渲染以校验错误结束，带出第一个找不到的占位符名。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AIPromptTemplateVectorTests
{
    /// <summary>
    /// 回放时模板用的名字
    /// </summary>
    private const string TemplateName = "vectors";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly PromptVectors _vectors = SpecVectors.Read<PromptVectors>("ai", "prompt-templates.json");

    [Fact]
    public void Render_GivesTheTextOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var rendered = new AIPromptTemplate(TemplateName, vector.Template).Render(Arguments(vector.Arguments));
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
    public void Render_FailsWithTheMissingArgumentOfEveryCase()
    {
        Assert.Equal(AIErrors.RequestInvalid, _vectors.Code);
        Assert.Equal(AIMessages.PromptArgumentMissing, _vectors.MessageKey);
        Assert.NotEmpty(_vectors.Missing);
        var failures = new List<string>();
        foreach (var vector in _vectors.Missing)
        {
            var rendered = new AIPromptTemplate(TemplateName, vector.Template).Render(Arguments(vector.Arguments));
            if (rendered.TryGetValue(out var text))
            {
                failures.Add($"{vector.Name}: rendered '{text}', the vector has a failure with '{vector.Argument}' missing");
                continue;
            }

            // 错误只带键与参数：参数 name 是模板的名字，argument 是第一个找不到参数的占位符名
            var error = rendered.Error!;
            var details = error.Details ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            if (error.Code != _vectors.Code || error.MessageKey != _vectors.MessageKey || !Equals(details.GetValueOrDefault("argument"), vector.Argument) || !Equals(details.GetValueOrDefault("name"), TemplateName))
            {
                failures.Add($"{vector.Name}: failed with '{error.Code}', '{error.MessageKey}' and argument '{details.GetValueOrDefault("argument")}', the vector has '{_vectors.Code}', '{_vectors.MessageKey}' and '{vector.Argument}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Placeholders_AreTheNamesRenderLooksUp()
    {
        // 占位符列表与渲染用同一套扫描：缺少参数的用例里，报出的那个名字就在列表里；没有占位符的模板列表为空
        Assert.All(_vectors.Missing, static vector => Assert.Contains(vector.Argument, new AIPromptTemplate(TemplateName, vector.Template).Placeholders));
        Assert.All(_vectors.Cases.Where(static vector => !vector.Template.Contains('{')), static vector => Assert.Empty(new AIPromptTemplate(TemplateName, vector.Template).Placeholders));
    }

    [Fact]
    public void TheVectors_CoverTheEscapesAndTheArguments()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：没有占位符、转义的花括号、同一个占位符出现两次
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Count == 0 && vector.Text == vector.Template);
        Assert.Contains(_vectors.Cases, static vector => vector.Template.Contains("{{", StringComparison.Ordinal) && vector.Template.Contains("}}", StringComparison.Ordinal));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Keys.Any(name => vector.Template.Split("{" + name + "}").Length > 2));

        // 值里带花括号的原样写出、多给的参数不用、值为空串
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Values.Any(value => value is not null && value.Contains('{') && vector.Text.Contains(value, StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Keys.Any(name => !vector.Template.Contains("{" + name + "}", StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Values.Any(static value => value is { Length: 0 }));

        // 值为 null、名字两侧带空白的占位符、没有闭合的 {（文本里留着它）
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Values.Any(static value => value is null));
        Assert.Contains(_vectors.Cases, static vector => vector.Arguments.Keys.Any(name => name.Length > 0 && vector.Template.Contains("{ " + name + " }", StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, static vector => vector.Template.Contains('{') && !vector.Template.Contains('}') && vector.Text == vector.Template);

        // 缺少参数：一个都没给的、给了一部分的、只差大小写的
        Assert.Contains(_vectors.Missing, static vector => vector.Arguments.Count == 0);
        Assert.Contains(_vectors.Missing, static vector => vector.Arguments.Count > 0 && vector.Template.Contains("{" + vector.Arguments.Keys.First() + "}", StringComparison.Ordinal));
        Assert.Contains(_vectors.Missing, static vector => vector.Arguments.Keys.Any(name => name != vector.Argument && string.Equals(name, vector.Argument, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 向量里的参数表
    /// </summary>
    /// <remarks>
    /// 按名字逐字比较（区分大小写），值是文本或 <see langword="null"/>
    /// </remarks>
    private static Dictionary<string, object?> Arguments(Dictionary<string, string?> arguments)
        => arguments.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value, StringComparer.Ordinal);

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Code">缺少参数时的错误码</param>
    /// <param name="MessageKey">缺少参数时的消息键</param>
    /// <param name="Cases">渲染得出文本的用例</param>
    /// <param name="Missing">缺少参数的用例</param>
    private sealed record PromptVectors(string Code, string MessageKey, PromptCase[] Cases, MissingCase[] Missing);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="Arguments">名字到值的参数</param>
    /// <param name="Text">渲染出的文本</param>
    private sealed record PromptCase(string Name, string Template, Dictionary<string, string?> Arguments, string Text);

    /// <summary>
    /// 一个缺少参数的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Template">模板</param>
    /// <param name="Arguments">名字到值的参数</param>
    /// <param name="Argument">第一个找不到参数的占位符名</param>
    private sealed record MissingCase(string Name, string Template, Dictionary<string, string?> Arguments, string Argument);
}
