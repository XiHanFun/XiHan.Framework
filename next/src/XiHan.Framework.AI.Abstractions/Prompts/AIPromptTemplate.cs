// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Prompts;

/// <summary>
/// 提示词模板：占位符 <c>{name}</c>，<c>{{</c> / <c>}}</c> 转义成字面量。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIPromptTemplate
{
    /// <summary>
    /// 创建。
    /// </summary>
    public AIPromptTemplate(string name, string template, string? description = null)
    {
        Name = AIIdentifiers.ValidateName(name, nameof(name));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Description = description;
    }

    /// <summary>
    /// 名字。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 模板。
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 模板引用的参数名（去重、按出现顺序）。
    /// </summary>
    public IReadOnlyList<string> Placeholders
    {
        get
        {
            var names = new List<string>();
            var position = 0;
            while (TryNextPlaceholder(Template, ref position, out var name))
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names;
        }
    }

    /// <summary>
    /// 渲染；缺参数 → <see cref="AIErrors.RequestInvalid"/>。值按不变文化转字符串，<see langword="null"/> 为空串。
    /// </summary>
    public Result<string> Render(IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var builder = new StringBuilder(Template.Length);
        var position = 0;
        while (position < Template.Length)
        {
            var c = Template[position];
            if (c == '{')
            {
                if (position + 1 < Template.Length && Template[position + 1] == '{')
                {
                    builder.Append('{');
                    position += 2;
                    continue;
                }

                var close = Template.IndexOf('}', position + 1);
                if (close < 0)
                {
                    builder.Append(Template, position, Template.Length - position);
                    break;
                }

                var name = Template[(position + 1)..close].Trim();
                if (arguments is null || !arguments.TryGetValue(name, out var value))
                {
                    return Error.Validation(AIErrors.RequestInvalid, AIMessages.PromptArgumentMissing, MessageArguments.Of(("name", Name), ("argument", name)));
                }

                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                position = close + 1;
                continue;
            }

            if (c == '}' && position + 1 < Template.Length && Template[position + 1] == '}')
            {
                builder.Append('}');
                position += 2;
                continue;
            }

            builder.Append(c);
            position++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// 从 <paramref name="position"/> 起找下一个占位符。
    /// </summary>
    private static bool TryNextPlaceholder(string template, ref int position, out string name)
    {
        name = string.Empty;
        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);
            if (open < 0)
            {
                position = template.Length;
                return false;
            }

            if (open + 1 < template.Length && template[open + 1] == '{')
            {
                position = open + 2;
                continue;
            }

            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                position = template.Length;
                return false;
            }

            name = template[(open + 1)..close].Trim();
            position = close + 1;
            return true;
        }

        return false;
    }
}
