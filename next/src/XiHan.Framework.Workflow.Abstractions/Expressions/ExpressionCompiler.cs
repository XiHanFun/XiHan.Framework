// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式小语言的编译器。
/// </summary>
/// <remarks>
/// 递归下降解析成闭包树。优先级从低到高：<c>||</c>、<c>&amp;&amp;</c>、<c>== !=</c>、<c>&lt; &lt;= &gt; &gt;=</c>、<c>+ -</c>、<c>* / %</c>、一元 <c>! -</c>、后缀 <c>.x</c> / <c>[i]</c>、原子。数字为 decimal；<c>+</c> 任一侧是字符串即拼接；<c>==</c> 跨数字类型宽松、字符串序数比较；未知根变量是错误，缺少的成员或键是 null。
/// </remarks>
internal sealed class ExpressionCompiler
{
    /// <summary>
    /// 源文本。
    /// </summary>
    private readonly string _text;

    /// <summary>
    /// 当前位置。
    /// </summary>
    private int _position;

    /// <summary>
    /// 创建。
    /// </summary>
    private ExpressionCompiler(string text)
    {
        _text = text;
    }

    /// <summary>
    /// 编译。
    /// </summary>
    /// <remarks>
    /// 语法错误抛 <see cref="ExpressionException"/>。
    /// </remarks>
    public static CompiledExpression Compile(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var compiler = new ExpressionCompiler(expression);
        compiler.SkipWhitespace();
        if (compiler._position >= expression.Length)
        {
            throw new ExpressionException("The expression is empty.");
        }

        var compiled = compiler.ParseOr();
        compiler.SkipWhitespace();
        if (compiler._position < expression.Length)
        {
            throw new ExpressionException($"Unexpected '{expression[compiler._position]}' at position {compiler._position}.");
        }

        return compiled;
    }

    /// <summary>
    /// <c>||</c>，短路，要求布尔操作数。
    /// </summary>
    private CompiledExpression ParseOr()
    {
        var left = ParseAnd();
        while (TryConsume("||"))
        {
            var l = left;
            var r = ParseAnd();
            left = scope => Boolean(l(scope), "||") || Boolean(r(scope), "||") ? True : False;
        }

        return left;
    }

    /// <summary>
    /// <c>&amp;&amp;</c>，短路，要求布尔操作数。
    /// </summary>
    private CompiledExpression ParseAnd()
    {
        var left = ParseEquality();
        while (TryConsume("&&"))
        {
            var l = left;
            var r = ParseEquality();
            left = scope => Boolean(l(scope), "&&") && Boolean(r(scope), "&&") ? True : False;
        }

        return left;
    }

    /// <summary>
    /// <c>==</c> 与 <c>!=</c>。
    /// </summary>
    private CompiledExpression ParseEquality()
    {
        var left = ParseComparison();
        while (true)
        {
            if (TryConsume("=="))
            {
                var l = left;
                var r = ParseComparison();
                left = scope => AreEqual(l(scope), r(scope)) ? True : False;
            }
            else if (TryConsume("!="))
            {
                var l = left;
                var r = ParseComparison();
                left = scope => AreEqual(l(scope), r(scope)) ? False : True;
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// <c>&lt;</c>、<c>&lt;=</c>、<c>&gt;</c>、<c>&gt;=</c>。
    /// </summary>
    private CompiledExpression ParseComparison()
    {
        var left = ParseAdditive();
        while (true)
        {
            string? op = null;
            foreach (var candidate in new[] { "<=", ">=", "<", ">" })
            {
                if (TryConsume(candidate))
                {
                    op = candidate;
                    break;
                }
            }

            if (op is null)
            {
                return left;
            }

            var l = left;
            var r = ParseAdditive();
            var captured = op;
            left = scope =>
            {
                var order = Compare(l(scope), r(scope), captured);
                var holds = captured switch
                {
                    "<" => order < 0,
                    "<=" => order <= 0,
                    ">" => order > 0,
                    _ => order >= 0,
                };
                return holds ? True : False;
            };
        }
    }

    /// <summary>
    /// <c>+</c> 与 <c>-</c>。
    /// </summary>
    private CompiledExpression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (true)
        {
            if (TryConsume("+"))
            {
                var l = left;
                var r = ParseMultiplicative();
                left = scope => Add(l(scope), r(scope));
            }
            else if (TryConsume("-"))
            {
                var l = left;
                var r = ParseMultiplicative();
                left = scope => JsonValue.Create(Number(l(scope), "-") - Number(r(scope), "-"));
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// <c>*</c>、<c>/</c>、<c>%</c>。
    /// </summary>
    private CompiledExpression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (true)
        {
            if (TryConsume("*"))
            {
                var l = left;
                var r = ParseUnary();
                left = scope => JsonValue.Create(Number(l(scope), "*") * Number(r(scope), "*"));
            }
            else if (TryConsume("/"))
            {
                var l = left;
                var r = ParseUnary();
                left = scope =>
                {
                    var divisor = Number(r(scope), "/");
                    return divisor == 0 ? throw new ExpressionException("Division by zero.") : JsonValue.Create(Number(l(scope), "/") / divisor);
                };
            }
            else if (TryConsume("%"))
            {
                var l = left;
                var r = ParseUnary();
                left = scope =>
                {
                    var divisor = Number(r(scope), "%");
                    return divisor == 0 ? throw new ExpressionException("Division by zero.") : JsonValue.Create(Number(l(scope), "%") % divisor);
                };
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// 一元 <c>!</c> 与 <c>-</c>。
    /// </summary>
    private CompiledExpression ParseUnary()
    {
        if (TryConsume("!"))
        {
            var operand = ParseUnary();
            return scope => Boolean(operand(scope), "!") ? False : True;
        }

        if (TryConsume("-"))
        {
            var operand = ParseUnary();
            return scope => JsonValue.Create(-Number(operand(scope), "-"));
        }

        return ParsePostfix();
    }

    /// <summary>
    /// 后缀：成员访问与索引。
    /// </summary>
    private CompiledExpression ParsePostfix()
    {
        var target = ParsePrimary();
        while (true)
        {
            SkipWhitespace();
            if (TryConsume("."))
            {
                var member = ReadIdentifier() ?? throw new ExpressionException($"Expected a member name at position {_position}.");
                var t = target;
                target = scope => Member(t(scope), member);
            }
            else if (TryConsume("["))
            {
                var index = ParseOr();
                SkipWhitespace();
                if (!TryConsume("]"))
                {
                    throw new ExpressionException($"Expected ']' at position {_position}.");
                }

                var t = target;
                target = scope => Index(t(scope), index(scope));
            }
            else
            {
                return target;
            }
        }
    }

    /// <summary>
    /// 原子：数字、字符串、<c>true</c> / <c>false</c> / <c>null</c>、标识符或函数调用、括号。
    /// </summary>
    private CompiledExpression ParsePrimary()
    {
        SkipWhitespace();
        if (_position >= _text.Length)
        {
            throw new ExpressionException("Unexpected end of expression.");
        }

        var c = _text[_position];
        if (c == '(')
        {
            _position++;
            var inner = ParseOr();
            SkipWhitespace();
            if (!TryConsume(")"))
            {
                throw new ExpressionException($"Expected ')' at position {_position}.");
            }

            return inner;
        }

        if (c is '\'' or '"')
        {
            var text = ReadString(c);
            var value = JsonValue.Create(text);
            return _ => value.DeepClone();
        }

        if (char.IsDigit(c) || (c == '.' && _position + 1 < _text.Length && char.IsDigit(_text[_position + 1])))
        {
            var number = ReadNumber();
            return _ => JsonValue.Create(number);
        }

        var identifier = ReadIdentifier() ?? throw new ExpressionException($"Unexpected '{c}' at position {_position}.");
        switch (identifier)
        {
            case "true":
                return _ => True;
            case "false":
                return _ => False;
            case "null":
                return _ => null;
        }

        SkipWhitespace();
        if (TryConsume("("))
        {
            var arguments = new List<CompiledExpression>();
            SkipWhitespace();
            if (!TryConsume(")"))
            {
                do
                {
                    arguments.Add(ParseOr());
                    SkipWhitespace();
                }
                while (TryConsume(","));
                if (!TryConsume(")"))
                {
                    throw new ExpressionException($"Expected ')' at position {_position}.");
                }
            }

            return BindFunction(identifier, arguments);
        }

        var name = identifier;
        // 未知根变量是错误：条件里写错变量名应当暴露，而不是悄悄变成 null
        return scope => scope.TryResolve(name, out var value) ? value : throw new ExpressionException($"Unknown variable '{name}'.");
    }

    /// <summary>
    /// 内置函数（名字不区分大小写）。
    /// </summary>
    /// <remarks>
    /// 参数个数在编译期校验。
    /// </remarks>
    private static CompiledExpression BindFunction(string name, List<CompiledExpression> args)
    {
        static void Arity(string name, List<CompiledExpression> args, int min, int max)
        {
            if (args.Count < min || args.Count > max)
            {
                throw new ExpressionException($"Function '{name}' expects {(min == max ? min.ToString(CultureInfo.InvariantCulture) : $"{min} to {max}")} argument(s).");
            }
        }

        switch (name.ToLowerInvariant())
        {
            case "len":
                Arity(name, args, 1, 1);
                return scope => args[0](scope) switch
                {
                    null => JsonValue.Create(0),
                    JsonArray array => JsonValue.Create(array.Count),
                    JsonObject obj => JsonValue.Create(obj.Count),
                    JsonValue value => JsonValue.Create(WorkflowValues.AsString(value).Length),
                    _ => JsonValue.Create(0),
                };
            case "contains":
                Arity(name, args, 2, 2);
                return scope =>
                {
                    var haystack = args[0](scope);
                    var needle = args[1](scope);
                    if (haystack is JsonArray array)
                    {
                        foreach (var item in array)
                        {
                            if (AreEqual(item, needle))
                            {
                                return True;
                            }
                        }

                        return False;
                    }

                    return Text(haystack).Contains(Text(needle), StringComparison.Ordinal) ? True : False;
                };
            case "startswith":
                Arity(name, args, 2, 2);
                return scope => Text(args[0](scope)).StartsWith(Text(args[1](scope)), StringComparison.Ordinal) ? True : False;
            case "endswith":
                Arity(name, args, 2, 2);
                return scope => Text(args[0](scope)).EndsWith(Text(args[1](scope)), StringComparison.Ordinal) ? True : False;
            case "upper":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Text(args[0](scope)).ToUpperInvariant());
            case "lower":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Text(args[0](scope)).ToLowerInvariant());
            case "trim":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Text(args[0](scope)).Trim());
            case "isnullorempty":
                Arity(name, args, 1, 1);
                return scope => args[0](scope) switch
                {
                    null => True,
                    JsonArray array => array.Count == 0 ? True : False,
                    JsonObject obj => obj.Count == 0 ? True : False,
                    var value => Text(value).Length == 0 ? True : False,
                };
            case "abs":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Math.Abs(Number(args[0](scope), "abs")));
            case "min":
                Arity(name, args, 2, 2);
                return scope => JsonValue.Create(Math.Min(Number(args[0](scope), "min"), Number(args[1](scope), "min")));
            case "max":
                Arity(name, args, 2, 2);
                return scope => JsonValue.Create(Math.Max(Number(args[0](scope), "max"), Number(args[1](scope), "max")));
            case "round":
                Arity(name, args, 1, 2);
                return scope =>
                {
                    var digits = args.Count == 2 ? (int)Number(args[1](scope), "round") : 0;
                    return JsonValue.Create(Math.Round(Number(args[0](scope), "round"), digits, MidpointRounding.AwayFromZero));
                };
            case "tonumber":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Number(args[0](scope), "toNumber"));
            case "tostring":
                Arity(name, args, 1, 1);
                return scope => JsonValue.Create(Text(args[0](scope)));
            case "now":
                Arity(name, args, 0, 0);
                return scope => JsonValue.Create(scope.Now);
            case "date":
                Arity(name, args, 1, 1);
                return scope =>
                {
                    var text = Text(args[0](scope));
                    return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
                        ? JsonValue.Create(when)
                        : throw new ExpressionException($"'{text}' is not a date.");
                };
            default:
                throw new ExpressionException($"Unknown function '{name}'.");
        }
    }

    /// <summary>
    /// 布尔字面量 <c>true</c>（每次新建，节点不能复用父）。
    /// </summary>
    private static JsonNode True => JsonValue.Create(true);

    /// <summary>
    /// 布尔字面量 <c>false</c>。
    /// </summary>
    private static JsonNode False => JsonValue.Create(false);

    /// <summary>
    /// 成员访问：对象取属性（没有为 null），null 为 null，其他类型是错误。
    /// </summary>
    private static JsonNode? Member(JsonNode? target, string member) => target switch
    {
        null => null,
        JsonObject obj => obj.TryGetPropertyValue(member, out var value) ? value : null,
        _ => throw new ExpressionException($"Cannot read member '{member}' of a non-object value."),
    };

    /// <summary>
    /// 索引：数组按整数（越界是错误），对象按字符串键（没有为 null），null 为 null。
    /// </summary>
    private static JsonNode? Index(JsonNode? target, JsonNode? index)
    {
        switch (target)
        {
            case null:
                return null;
            case JsonArray array:
                var position = (int)Number(index, "[]");
                return position >= 0 && position < array.Count ? array[position] : throw new ExpressionException($"Index {position} is out of range (length {array.Count}).");
            case JsonObject obj:
                return obj.TryGetPropertyValue(Text(index), out var value) ? value : null;
            default:
                throw new ExpressionException("Cannot index a scalar value.");
        }
    }

    /// <summary>
    /// 加法：任一侧是字符串即拼接，否则数字相加。
    /// </summary>
    private static JsonValue Add(JsonNode? left, JsonNode? right)
    {
        if (IsString(left) || IsString(right))
        {
            return JsonValue.Create(Text(left) + Text(right));
        }

        return JsonValue.Create(Number(left, "+") + Number(right, "+"));
    }

    /// <summary>
    /// 宽松相等。
    /// </summary>
    /// <remarks>
    /// 都为 null 相等；都是数字按数值；都是布尔按值；其余按字符串序数比较。
    /// </remarks>
    private static bool AreEqual(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is JsonValue l && right is JsonValue r)
        {
            if (WorkflowValues.TryBool(l, out var lb) && WorkflowValues.TryBool(r, out var rb) && !IsString(l) && !IsString(r))
            {
                return lb == rb;
            }

            if (WorkflowValues.TryDecimal(l, out var ln) && WorkflowValues.TryDecimal(r, out var rn))
            {
                return ln == rn;
            }

            return string.Equals(WorkflowValues.AsString(l), WorkflowValues.AsString(r), StringComparison.Ordinal);
        }

        return JsonNode.DeepEquals(left, right);
    }

    /// <summary>
    /// 排序比较：数字按数值，时间按时刻，其余按字符串序数。
    /// </summary>
    private static int Compare(JsonNode? left, JsonNode? right, string op)
    {
        if (left is JsonValue l && right is JsonValue r)
        {
            if (WorkflowValues.TryDecimal(l, out var ln) && WorkflowValues.TryDecimal(r, out var rn))
            {
                return ln.CompareTo(rn);
            }

            if (WorkflowValues.TryConvert<DateTimeOffset>(l, out var lt) && WorkflowValues.TryConvert<DateTimeOffset>(r, out var rt))
            {
                return lt.CompareTo(rt);
            }

            return string.CompareOrdinal(WorkflowValues.AsString(l), WorkflowValues.AsString(r));
        }

        throw new ExpressionException($"Operator '{op}' needs two scalar operands.");
    }

    /// <summary>
    /// 要求布尔。
    /// </summary>
    private static bool Boolean(JsonNode? node, string op)
        => node is JsonValue value && !IsString(value) && WorkflowValues.TryBool(value, out var flag) ? flag : throw new ExpressionException($"Operator '{op}' needs boolean operands.");

    /// <summary>
    /// 要求数字（字符串形式的数字也接受）。
    /// </summary>
    private static decimal Number(JsonNode? node, string op)
        => node is JsonValue value && WorkflowValues.TryDecimal(value, out var number) ? number : throw new ExpressionException($"Operator '{op}' needs numeric operands.");

    /// <summary>
    /// 任意值的字符串形式：null 为空串，标量按 <see cref="WorkflowValues.AsString"/>，容器为 JSON 文本。
    /// </summary>
    private static string Text(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue value => WorkflowValues.AsString(value),
        _ => node.ToJsonString(),
    };

    /// <summary>
    /// 是否字符串标量。
    /// </summary>
    private static bool IsString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out _);

    /// <summary>
    /// 跳过空白。
    /// </summary>
    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    /// 若当前位置是 <paramref name="token"/> 则消费。
    /// </summary>
    /// <remarks>
    /// 运算符前先跳空白。
    /// </remarks>
    private bool TryConsume(string token)
    {
        SkipWhitespace();
        if (string.CompareOrdinal(_text, _position, token, 0, token.Length) != 0)
        {
            return false;
        }

        // 单字符运算符不能吃掉双字符运算符的前半（"<" 与 "<="、"=" 与 "=="、"!" 与 "!="）
        if (token.Length == 1 && _position + 1 < _text.Length && token[0] is '<' or '>' or '!' or '=' && _text[_position + 1] == '=')
        {
            return false;
        }

        _position += token.Length;
        return true;
    }

    /// <summary>
    /// 读标识符（字母、数字、下划线，首字符非数字）。
    /// </summary>
    /// <remarks>
    /// 不是标识符返回 <see langword="null"/>。
    /// </remarks>
    private string? ReadIdentifier()
    {
        SkipWhitespace();
        var start = _position;
        if (_position >= _text.Length || !(char.IsLetter(_text[_position]) || _text[_position] == '_'))
        {
            return null;
        }

        while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
        {
            _position++;
        }

        return _text[start.._position];
    }

    /// <summary>
    /// 读数字（十进制，可带小数）。
    /// </summary>
    private decimal ReadNumber()
    {
        var start = _position;
        while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
        {
            _position++;
        }

        var text = _text[start.._position];
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ExpressionException($"'{text}' is not a number.");
    }

    /// <summary>
    /// 读引号字符串，支持 <c>\n \t \r \\ \' \"</c> 转义。
    /// </summary>
    private string ReadString(char quote)
    {
        _position++;
        var builder = new StringBuilder();
        while (_position < _text.Length)
        {
            var c = _text[_position++];
            if (c == quote)
            {
                return builder.ToString();
            }

            if (c == '\\' && _position < _text.Length)
            {
                var escaped = _text[_position++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => escaped,
                });
                continue;
            }

            builder.Append(c);
        }

        throw new ExpressionException("Unterminated string literal.");
    }
}
