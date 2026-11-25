// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式小语言的编译器
/// </summary>
/// <remarks>
/// 递归下降解析成闭包树，文法与每个运算符、函数的语义照 RFC-0021「表达式」（ADR-0287）。优先级从低到高：<c>||</c>、<c>&amp;&amp;</c>、<c>== !=</c>、<c>&lt; &lt;= &gt; &gt;=</c>、<c>+ -</c>、<c>* / %</c>、一元 <c>! -</c>、后缀 <c>.x</c> / <c>[i]</c>、原子。数字为 decimal；<c>+</c> 任一侧是字符串即拼接；两边都是数字或数字文本时按数值比较，其余按文本的序数比较；未知根变量是错误，缺少的成员或键是 null。二元运算符与函数先从左到右求值全部操作数，再核对类型（<c>&amp;&amp;</c> 与 <c>||</c> 短路）
/// 长度不超过 <see cref="MaxLength"/>，嵌套不超过 <see cref="MaxDepth"/>（ADR-0120）；<c>+</c> 拼出的字符串与模板渲染出的文本不超过 <see cref="MaxStringLength"/>（ADR-0182、ADR-0287）；一次求值的工作量不超过 <see cref="ExpressionRun.MaxWork"/>：每个节点开始求值时记一步，运算在核对操作数之前记它读的字符串、数字与容器（ADR-0296、ADR-0303）；表达式得出的数字去掉 decimal 内部的结尾的零，写成 JSON 就是数字的文本（ADR-0303）
/// </remarks>
internal sealed class ExpressionCompiler
{
    /// <summary>
    /// <c>+</c> 拼出的字符串与模板渲染出的文本的长度上限（UTF-16 码元）
    /// </summary>
    /// <remarks>
    /// 与 <c>Http</c> 活动保留的正文同量级。<c>s + s</c> 每求值一次长度翻倍，几十步就超过 1 GB：在分配之前按两侧长度核对
    /// </remarks>
    public const int MaxStringLength = 4 * 1024 * 1024;

    /// <summary>
    /// 表达式的长度上限（UTF-16 码元，空白也算）
    /// </summary>
    /// <remarks>
    /// 也压住了求值的深度：链（<c>a + b + c</c>、<c>a.b.c</c>）解析时是循环，求值时闭包一层套一层，这个长度下是两千层左右
    /// </remarks>
    public const int MaxLength = 4000;

    /// <summary>
    /// 嵌套层数上限：括号、函数实参、索引、一元运算符都算
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// 数字最多的小数位数，也是 <c>round</c> 的小数位数的上限
    /// </summary>
    private const int MaxScale = 28;

    /// <summary>
    /// 源文本
    /// </summary>
    private readonly string _text;

    /// <summary>
    /// 当前位置
    /// </summary>
    private int _position;

    /// <summary>
    /// 当前的嵌套层数
    /// </summary>
    private int _depth;

    /// <summary>
    /// 创建
    /// </summary>
    private ExpressionCompiler(string text)
    {
        _text = text;
    }

    /// <summary>
    /// 编译
    /// </summary>
    /// <remarks>
    /// 语法错误抛 <see cref="ExpressionException"/>
    /// </remarks>
    public static CompiledExpression Compile(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression.Length > MaxLength)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionTooLong, MessageArguments.Of(("length", expression.Length), ("max", MaxLength)));
        }

        var compiler = new ExpressionCompiler(expression);
        compiler.SkipWhitespace();
        if (compiler._position >= expression.Length)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionEmpty);
        }

        var compiled = compiler.ParseOr();
        compiler.SkipWhitespace();
        if (compiler._position < expression.Length)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionUnexpectedCharacter, MessageArguments.Of(("character", compiler.CharacterAt(compiler._position)), ("position", compiler._position + 1)));
        }

        return compiled;
    }

    /// <summary>
    /// <c>||</c>，短路，要求布尔操作数
    /// </summary>
    private CompiledExpression ParseOr()
    {
        var left = ParseAnd();
        while (TryConsume("||"))
        {
            var l = left;
            var r = ParseAnd();
            left = run =>
            {
                // 步先于操作数记；短路没有求值的右操作数不记
                run.Step();
                return Boolean(l(run), "||") || Boolean(r(run), "||") ? True : False;
            };
        }

        return left;
    }

    /// <summary>
    /// <c>&amp;&amp;</c>，短路，要求布尔操作数
    /// </summary>
    private CompiledExpression ParseAnd()
    {
        var left = ParseEquality();
        while (TryConsume("&&"))
        {
            var l = left;
            var r = ParseEquality();
            left = run =>
            {
                run.Step();
                return Boolean(l(run), "&&") && Boolean(r(run), "&&") ? True : False;
            };
        }

        return left;
    }

    /// <summary>
    /// <c>==</c> 与 <c>!=</c>
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
                left = Binary(l, r, static (run, a, b) => Equal(run, a, b) ? True : False);
            }
            else if (TryConsume("!="))
            {
                var l = left;
                var r = ParseComparison();
                left = Binary(l, r, static (run, a, b) => Equal(run, a, b) ? False : True);
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// <c>&lt;</c>、<c>&lt;=</c>、<c>&gt;</c>、<c>&gt;=</c>
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
            left = Binary(l, r, (run, a, b) =>
            {
                // 两侧可能当数字、时间或文本读：字符串与数字要读内容（数字要从它的写法读出值）
                var (first, second) = ReadScalars(run, a, b);
                var order = Compare(first, second, captured);
                var holds = captured switch
                {
                    "<" => order < 0,
                    "<=" => order <= 0,
                    ">" => order > 0,
                    _ => order >= 0,
                };
                return holds ? True : False;
            });
        }
    }

    /// <summary>
    /// <c>+</c> 与 <c>-</c>
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
                left = Binary(l, r, Add);
            }
            else if (TryConsume("-"))
            {
                var l = left;
                var r = ParseMultiplicative();
                left = Binary(l, r, Subtract);
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// <c>*</c>、<c>/</c>、<c>%</c>
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
                left = Binary(l, r, Multiply);
            }
            else if (TryConsume("/"))
            {
                var l = left;
                var r = ParseUnary();
                left = Binary(l, r, Divide);
            }
            else if (TryConsume("%"))
            {
                var l = left;
                var r = ParseUnary();
                left = Binary(l, r, Remainder);
            }
            else
            {
                return left;
            }
        }
    }

    /// <summary>
    /// 一元 <c>!</c> 与 <c>-</c>
    /// </summary>
    private CompiledExpression ParseUnary()
    {
        if (TryConsume("!"))
        {
            EnterNesting();
            var operand = ParseUnary();
            _depth--;
            return run =>
            {
                run.Step();
                return Boolean(operand(run), "!") ? False : True;
            };
        }

        if (TryConsume("-"))
        {
            EnterNesting();
            var operand = ParseUnary();
            _depth--;
            return run =>
            {
                run.Step();
                var value = run.ReadScalar(operand(run));
                return NumberValue(-Number(value, "-"));
            };
        }

        return ParsePostfix();
    }

    /// <summary>
    /// 后缀：成员访问与索引
    /// </summary>
    private CompiledExpression ParsePostfix()
    {
        var target = ParsePrimary();
        while (true)
        {
            SkipWhitespace();
            if (TryConsume("."))
            {
                var member = ReadIdentifier() ?? throw new ExpressionException(WorkflowMessages.ExpressionMemberExpected, MessageArguments.Of(("position", _position + 1)));
                var t = target;
                target = run =>
                {
                    run.Step();
                    return Member(t(run), member);
                };
            }
            else if (TryConsume("["))
            {
                EnterNesting();
                var index = ParseOr();
                _depth--;
                SkipWhitespace();
                if (!TryConsume("]"))
                {
                    throw new ExpressionException(WorkflowMessages.ExpressionTokenExpected, MessageArguments.Of(("token", "]"), ("position", _position + 1)));
                }

                var t = target;
                target = run =>
                {
                    run.Step();
                    var value = t(run);
                    var key = index(run);
                    return Index(run, value, key);
                };
            }
            else
            {
                return target;
            }
        }
    }

    /// <summary>
    /// 原子：数字、字符串、<c>true</c> / <c>false</c> / <c>null</c>、标识符或函数调用、括号
    /// </summary>
    private CompiledExpression ParsePrimary()
    {
        SkipWhitespace();
        if (_position >= _text.Length)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionUnexpectedEnd);
        }

        var c = _text[_position];
        if (c == '(')
        {
            _position++;
            EnterNesting();
            var inner = ParseOr();
            _depth--;
            SkipWhitespace();
            if (!TryConsume(")"))
            {
                throw new ExpressionException(WorkflowMessages.ExpressionTokenExpected, MessageArguments.Of(("token", ")"), ("position", _position + 1)));
            }

            return inner;
        }

        if (c is '\'' or '"')
        {
            var text = ReadString(c);
            var value = JsonValue.Create(text);
            return run =>
            {
                run.Step();
                return value.DeepClone();
            };
        }

        if (char.IsDigit(c) || (c == '.' && _position + 1 < _text.Length && char.IsDigit(_text[_position + 1])))
        {
            // 字面量的数字也不带内部的小数位数：1.50 与 1.5 写成 JSON 是同一段文本（ADR-0303）
            var number = WorkflowNumberText.Normalize(ReadNumber());
            return run =>
            {
                run.Step();
                return JsonValue.Create(number);
            };
        }

        var identifier = ReadIdentifier() ?? throw new ExpressionException(WorkflowMessages.ExpressionUnexpectedCharacter, MessageArguments.Of(("character", CharacterAt(_position)), ("position", _position + 1)));
        switch (identifier)
        {
            case "true":
                return static run =>
                {
                    run.Step();
                    return True;
                };
            case "false":
                return static run =>
                {
                    run.Step();
                    return False;
                };
            case "null":
                return static run =>
                {
                    run.Step();
                    return null;
                };
        }

        SkipWhitespace();
        if (TryConsume("("))
        {
            var arguments = new List<CompiledExpression>();
            SkipWhitespace();
            if (!TryConsume(")"))
            {
                EnterNesting();
                do
                {
                    arguments.Add(ParseOr());
                    SkipWhitespace();
                }
                while (TryConsume(","));
                _depth--;
                if (!TryConsume(")"))
                {
                    throw new ExpressionException(WorkflowMessages.ExpressionTokenExpected, MessageArguments.Of(("token", ")"), ("position", _position + 1)));
                }
            }

            var call = BindFunction(identifier, arguments);
            return run =>
            {
                run.Step();
                return call(run);
            };
        }

        var name = identifier;
        // 未知根变量是错误：条件里写错变量名应当暴露，而不是悄悄变成 null
        return run =>
        {
            run.Step();
            return run.Scope.TryResolve(name, out var value) ? value : throw new ExpressionException(WorkflowMessages.ExpressionUnknownVariable, MessageArguments.Of(("name", name)));
        };
    }

    /// <summary>
    /// 内置函数（名字不区分大小写）
    /// </summary>
    /// <remarks>
    /// 参数个数在编译期校验。求值时先从左到右求值全部实参，再记它读的实参的工作量（ADR-0296），再核对类型；调用节点自己的一步由调用方记
    /// </remarks>
    private static CompiledExpression BindFunction(string name, List<CompiledExpression> args)
    {
        static void Arity(string name, List<CompiledExpression> args, int min, int max)
        {
            if (args.Count < min || args.Count > max)
            {
                throw min == max
                    ? new ExpressionException(WorkflowMessages.ExpressionArgumentCount, MessageArguments.Of(("function", name), ("count", min)))
                    : new ExpressionException(WorkflowMessages.ExpressionArgumentRange, MessageArguments.Of(("function", name), ("min", min), ("max", max)));
            }
        }

        switch (name.ToLowerInvariant())
        {
            case "len":
                Arity(name, args, 1, 1);
                return run =>
                {
                    // 字符串的长度要取出全文才数得出 UTF-16 码元（ADR-0309），量长度取出的那份文本就是数的那份，不取第二遍；数字的长度是它的数字的文本的长度，要先从它的写法读出值（ADR-0303）；容器的长度是个数，不读
                    var value = args[0](run);
                    return value switch
                    {
                        null => JsonValue.Create(0),
                        JsonArray array => JsonValue.Create(array.Count),
                        JsonObject obj => JsonValue.Create(obj.Count),
                        _ => JsonValue.Create(run.ReadText(value).Length),
                    };
                };
            case "contains":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var haystack = args[0](run);
                    var needle = args[1](run);
                    if (haystack is JsonArray array)
                    {
                        // 每个元素都与要找的值比一次：读数组一遍，再按元素个数读要找的值；要找的字符串只取出一次，每次比较都用这一份
                        run.Read(array);
                        var compared = run.ReadTimes(needle, array.Count);
                        foreach (var item in array)
                        {
                            if (AreEqual(item, compared))
                            {
                                return True;
                            }
                        }

                        return False;
                    }

                    var (text, part) = ReadTexts(run, haystack, needle);
                    return text.Contains(part, StringComparison.Ordinal) ? True : False;
                };
            case "startswith":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var (text, prefix) = ReadTexts(run, args[0](run), args[1](run));
                    return text.StartsWith(prefix, StringComparison.Ordinal) ? True : False;
                };
            case "endswith":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var (text, suffix) = ReadTexts(run, args[0](run), args[1](run));
                    return text.EndsWith(suffix, StringComparison.Ordinal) ? True : False;
                };
            case "upper":
                Arity(name, args, 1, 1);
                // 逐个码位的简单大写映射，与关键字的折叠同一份映射（ADR-0303）
                return run => JsonValue.Create(WorkflowCaseMapping.ToUpper(run.ReadText(args[0](run))));
            case "lower":
                Arity(name, args, 1, 1);
                return run => JsonValue.Create(WorkflowCaseMapping.ToLower(run.ReadText(args[0](run))));
            case "trim":
                Arity(name, args, 1, 1);
                return run => JsonValue.Create(run.ReadText(args[0](run)).Trim());
            case "isnullorempty":
                Arity(name, args, 1, 1);
                return run =>
                {
                    // 只读到能判定为止（ADR-0309）：null 与容器看类型与个数，布尔与数字的文本总不是空的，字符串只看有没有第一个码元
                    var value = args[0](run);
                    return value switch
                    {
                        null => True,
                        JsonArray array => array.Count == 0 ? True : False,
                        JsonObject obj => obj.Count == 0 ? True : False,
                        JsonValue scalar when scalar.GetValueKind() == JsonValueKind.String => run.ReadEmptiness(scalar) ? True : False,
                        _ => False,
                    };
                };
            case "abs":
                Arity(name, args, 1, 1);
                return run => NumberValue(Math.Abs(ReadNumber(run, args[0](run), "abs")));
            case "min":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var (first, second) = ReadScalars(run, args[0](run), args[1](run));
                    return NumberValue(Math.Min(Number(first, "min"), Number(second, "min")));
                };
            case "max":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var (first, second) = ReadScalars(run, args[0](run), args[1](run));
                    return NumberValue(Math.Max(Number(first, "max"), Number(second, "max")));
                };
            case "round":
                Arity(name, args, 1, 2);
                return run =>
                {
                    var (value, places) = ReadScalars(run, args[0](run), args.Count == 2 ? args[1](run) : null);
                    var number = Number(value, "round");
                    var digits = 0m;
                    if (args.Count == 2)
                    {
                        // 小数位数截去小数部分；decimal 只有 0 到 28 位小数，范围之外不交给平台去抛异常
                        digits = decimal.Truncate(Number(places, "round"));
                        if (digits < 0 || digits > MaxScale)
                        {
                            throw new ExpressionException(WorkflowMessages.ExpressionNumberOutOfRange, MessageArguments.Of(("operator", "round")));
                        }
                    }

                    return NumberValue(Math.Round(number, (int)digits, MidpointRounding.AwayFromZero));
                };
            case "format":
                Arity(name, args, 2, 2);
                return run =>
                {
                    var (value, places) = ReadScalars(run, args[0](run), args[1](run));
                    var number = Number(value, "format");
                    var digits = Number(places, "format");

                    // 小数位数必须是 0 到 28 的整数：与 round 不同，2.5 位小数没有意义，不截去小数部分将就
                    if (digits != decimal.Truncate(digits) || digits < 0 || digits > MaxScale)
                    {
                        throw new ExpressionException(WorkflowMessages.ExpressionNumberOutOfRange, MessageArguments.Of(("operator", "format")));
                    }

                    // 舍入与 round 相同（正好一半时远离零），结果是字符串：定长的文本不是数字，带结尾的零
                    return JsonValue.Create(WorkflowNumberText.Format(Math.Round(number, (int)digits, MidpointRounding.AwayFromZero), (int)digits));
                };
            case "tonumber":
                Arity(name, args, 1, 1);
                return run => NumberValue(ReadNumber(run, args[0](run), "toNumber"));
            case "tostring":
                Arity(name, args, 1, 1);
                return run => JsonValue.Create(run.ReadText(args[0](run)));
            case "now":
                Arity(name, args, 0, 0);
                // 时间在值模型里是字符串：写成定宽的规范文本，过不过存储都是同一个值
                return static run => JsonValue.Create(WorkflowTimeText.Format(run.Scope.Now));
            case "date":
                Arity(name, args, 1, 1);
                return run =>
                {
                    var text = run.ReadText(args[0](run));
                    return WorkflowTimeText.TryParse(text, out var when)
                        ? JsonValue.Create(WorkflowTimeText.Format(when))
                        : throw new ExpressionException(WorkflowMessages.ExpressionNotDate, MessageArguments.Of(("value", ExpressionException.Echo(text))));
                };
            default:
                throw new ExpressionException(WorkflowMessages.ExpressionUnknownFunction, MessageArguments.Of(("function", name)));
        }
    }

    /// <summary>
    /// 布尔字面量 <c>true</c>（每次新建，节点不能复用父）
    /// </summary>
    private static JsonNode True => JsonValue.Create(true);

    /// <summary>
    /// 布尔字面量 <c>false</c>
    /// </summary>
    private static JsonNode False => JsonValue.Create(false);

    /// <summary>
    /// 成员访问：对象取属性（没有为 null），null 为 null，其他类型是错误
    /// </summary>
    private static JsonNode? Member(JsonNode? target, string member) => target switch
    {
        null => null,
        JsonObject obj => WorkflowValues.TryMember(obj, member, out var value) ? value : null,
        _ => throw new ExpressionException(WorkflowMessages.ExpressionMemberOfNonObject, MessageArguments.Of(("member", member))),
    };

    /// <summary>
    /// 索引：数组按整数（越界是错误），对象按字符串键（没有为 null），null 为 null
    /// </summary>
    /// <remarks>
    /// 先记读的下标，再核对：被索引的是数组时读字符串与数字的下标（当数字读），是对象时读下标（取它的文本作键），是 null 或标量时不读。数组的下标截去小数部分；负数、不小于长度的、连整数都放不下的都是越界，转换的溢出不逃出去
    /// </remarks>
    private static JsonNode? Index(ExpressionRun run, JsonNode? target, JsonNode? index)
    {
        switch (target)
        {
            case null:
                return null;
            case JsonArray array:
                var position = decimal.Truncate(Number(run.ReadScalar(index), "[]"));
                if (position >= 0 && position < array.Count)
                {
                    return array[(int)position];
                }

                // 参数里的下标是截断之后的整数：放得进 int 的照旧是 int，放不下的照原数
                var reported = position >= int.MinValue && position <= int.MaxValue ? (object)(int)position : position;
                throw new ExpressionException(WorkflowMessages.ExpressionIndexOutOfRange, MessageArguments.Of(("index", reported), ("length", array.Count)));
            case JsonObject obj:
                return WorkflowValues.TryMember(obj, run.ReadText(index), out var value) ? value : null;
            default:
                throw new ExpressionException(WorkflowMessages.ExpressionIndexOfScalar);
        }
    }

    /// <summary>
    /// 加法：任一侧是字符串即拼接，否则数字相加
    /// </summary>
    /// <remarks>
    /// 拼接读两侧（ADR-0296），工作量先于长度的核对记；加法读两侧里的数字（ADR-0303）：变量里的数字要从它的写法读出值
    /// </remarks>
    private static JsonValue Add(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        if (IsString(left) || IsString(right))
        {
            // 量长度取出的文本就是拼接用的文本：两侧各取一次
            var first = run.ReadText(left);
            var second = run.ReadText(right);
            // 先按长度核对再拼接：超长的结果一次也不分配
            var length = (long)first.Length + second.Length;
            if (length > MaxStringLength)
            {
                throw new ExpressionException(WorkflowMessages.ExpressionStringTooLong, MessageArguments.Of(("length", length), ("max", MaxStringLength)));
            }

            return JsonValue.Create(first + second);
        }

        var (l, r) = ReadScalars(run, left, right);
        var augend = Number(l, "+");
        var addend = Number(r, "+");
        try
        {
            return NumberValue(augend + addend);
        }
        catch (OverflowException)
        {
            throw OutOfRange("+");
        }
    }

    /// <summary>
    /// 减法
    /// </summary>
    private static JsonValue Subtract(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        var (l, r) = ReadScalars(run, left, right);
        var minuend = Number(l, "-");
        var subtrahend = Number(r, "-");
        try
        {
            return NumberValue(minuend - subtrahend);
        }
        catch (OverflowException)
        {
            throw OutOfRange("-");
        }
    }

    /// <summary>
    /// 乘法
    /// </summary>
    private static JsonValue Multiply(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        var (l, r) = ReadScalars(run, left, right);
        var multiplicand = Number(l, "*");
        var multiplier = Number(r, "*");
        try
        {
            return NumberValue(multiplicand * multiplier);
        }
        catch (OverflowException)
        {
            throw OutOfRange("*");
        }
    }

    /// <summary>
    /// 除法：两个操作数都是数字之后才看除数是不是零
    /// </summary>
    private static JsonValue Divide(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        var (l, r) = ReadScalars(run, left, right);
        var dividend = Number(l, "/");
        var divisor = Number(r, "/");
        if (divisor == 0)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionDivisionByZero);
        }

        try
        {
            return NumberValue(dividend / divisor);
        }
        catch (OverflowException)
        {
            throw OutOfRange("/");
        }
    }

    /// <summary>
    /// 取余：截断除法的余数，符号随被除数，结果总是放得下
    /// </summary>
    private static JsonValue Remainder(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        var (l, r) = ReadScalars(run, left, right);
        var dividend = Number(l, "%");
        var divisor = Number(r, "%");
        return divisor == 0 ? throw new ExpressionException(WorkflowMessages.ExpressionDivisionByZero) : NumberValue(dividend % divisor);
    }

    /// <summary>
    /// 二元运算符：记一步，从左到右求值两个操作数，再交给 <paramref name="apply"/>
    /// </summary>
    /// <remarks>
    /// 步先于操作数记（RFC-0021「长度与上限」，ADR-0296）；读与类型的核对在 <paramref name="apply"/> 里，读在前
    /// </remarks>
    private static CompiledExpression Binary(CompiledExpression left, CompiledExpression right, Func<ExpressionRun, JsonNode?, JsonNode?, JsonNode?> apply)
        => run =>
        {
            run.Step();
            var a = left(run);
            var b = right(run);
            return apply(run, a, b);
        };

    /// <summary>
    /// 相等：先按两侧的种类记读，再比较
    /// </summary>
    /// <remarks>
    /// 两侧都是标量时读字符串与数字（可能当数字或文本比）；两侧都是容器时要逐层比，读两侧；有一侧是 null，或容器对标量，不必看内容，不读
    /// </remarks>
    private static bool Equal(ExpressionRun run, JsonNode? left, JsonNode? right)
    {
        if (left is JsonValue && right is JsonValue)
        {
            (left, right) = ReadScalars(run, left, right);
        }
        else if (left is JsonArray or JsonObject && right is JsonArray or JsonObject)
        {
            run.Read(left);
            run.Read(right);
        }

        return AreEqual(left, right);
    }

    /// <summary>
    /// 记两个操作数里是字符串或数字的
    /// </summary>
    /// <returns>运算接着用的两个值（<see cref="ExpressionRun.ReadScalar"/>）</returns>
    private static (JsonNode? Left, JsonNode? Right) ReadScalars(ExpressionRun run, JsonNode? left, JsonNode? right)
        => (run.ReadScalar(left), run.ReadScalar(right));

    /// <summary>
    /// 记读两个实参并取出它们的文本
    /// </summary>
    private static (string First, string Second) ReadTexts(ExpressionRun run, JsonNode? first, JsonNode? second)
        => (run.ReadText(first), run.ReadText(second));

    /// <summary>
    /// 记读一个是字符串或数字的实参，再要求它能当数字
    /// </summary>
    private static decimal ReadNumber(ExpressionRun run, JsonNode? node, string op)
        => Number(run.ReadScalar(node), op);

    /// <summary>
    /// 表达式得出的数字
    /// </summary>
    /// <remarks>
    /// 去掉 decimal 内部记着的结尾的零（RFC-0021「表达式」的「数字的文本」，ADR-0303）：结果写成 JSON（存进变量、作为输出）就是数字的文本，存进去再读回来，写法与读取量都不变
    /// </remarks>
    private static JsonValue NumberValue(decimal number) => JsonValue.Create(WorkflowNumberText.Normalize(number));

    /// <summary>
    /// 结果超出数字范围的错误
    /// </summary>
    /// <remarks>
    /// decimal 放不下时平台抛 <see cref="OverflowException"/>：在这里换成表达式的错误，不让它逃出求值器
    /// </remarks>
    private static ExpressionException OutOfRange(string op)
        => new(WorkflowMessages.ExpressionNumberOutOfRange, MessageArguments.Of(("operator", op)));

    /// <summary>
    /// 宽松相等
    /// </summary>
    /// <remarks>
    /// 都为 null 相等，只有一边是 null 不等；都是布尔按值；都是数字或数字文本按数值；其余的标量按文本的序数比较；有一边是数组或对象时按结构深比较，里面的标量不做转换
    /// </remarks>
    private static bool AreEqual(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is JsonValue l && right is JsonValue r)
        {
            if (TryBoolean(l, out var lb) && TryBoolean(r, out var rb))
            {
                return lb == rb;
            }

            if (WorkflowValues.TryDecimal(l, out var ln) && WorkflowValues.TryDecimal(r, out var rn))
            {
                return ln == rn;
            }

            return string.Equals(WorkflowValues.AsString(l), WorkflowValues.AsString(r), StringComparison.Ordinal);
        }

        return DeepEqual(left, right);
    }

    /// <summary>
    /// 按结构深比较：至少一边是数组或对象
    /// </summary>
    /// <remarks>
    /// 数组长度相同且逐项相等；对象成员名相同（按序数，不看顺序）且各成员相等；里面的标量不做宽松转换，类型相同才可能相等，数字按数值；容器与标量不相等。对象的成员经 <see cref="WorkflowValues.TryMember"/> 取，不区分大小写的 <see cref="JsonObject"/> 也按序数比较成员名
    /// </remarks>
    private static bool DeepEqual(JsonNode? left, JsonNode? right)
    {
        switch (left, right)
        {
            case (null, null):
                return true;
            case (JsonArray a, JsonArray b):
                if (a.Count != b.Count)
                {
                    return false;
                }

                for (var index = 0; index < a.Count; index++)
                {
                    if (!DeepEqual(a[index], b[index]))
                    {
                        return false;
                    }
                }

                return true;
            case (JsonObject a, JsonObject b):
                if (a.Count != b.Count)
                {
                    return false;
                }

                foreach (var (name, value) in a)
                {
                    if (!WorkflowValues.TryMember(b, name, out var other) || !DeepEqual(value, other))
                    {
                        return false;
                    }
                }

                return true;
            case (JsonValue a, JsonValue b):
                return SameScalar(a, b);
            default:
                return false;
        }
    }

    /// <summary>
    /// 容器里的两个标量是否相同
    /// </summary>
    /// <remarks>
    /// 类型相同才可能相同：数字按放进数字之后的数值（写法不同、超出 28 位小数的照同一条规则舍入），字符串逐个码元，布尔按值。放不下的数字不在规定之内，交给平台按原文比
    /// </remarks>
    private static bool SameScalar(JsonValue left, JsonValue right)
    {
        var kind = left.GetValueKind();
        if (kind != right.GetValueKind())
        {
            return false;
        }

        return kind switch
        {
            JsonValueKind.True or JsonValueKind.False => true,
            JsonValueKind.String => string.Equals(WorkflowValues.AsString(left), WorkflowValues.AsString(right), StringComparison.Ordinal),
            JsonValueKind.Number when WorkflowValues.TryDecimal(left, out var l) && WorkflowValues.TryDecimal(right, out var r) => l == r,
            _ => JsonNode.DeepEquals(left, right),
        };
    }

    /// <summary>
    /// 排序比较：数字按数值，时间按时刻，其余按字符串序数
    /// </summary>
    /// <remarks>
    /// 两边都是数字或数字文本按数值；两边都是合文法的时间文本按时刻；其余按文本的序数（逐个 UTF-16 码元）。null、数组与对象不能比较
    /// </remarks>
    private static int Compare(JsonNode? left, JsonNode? right, string op)
    {
        if (left is JsonValue l && right is JsonValue r)
        {
            if (WorkflowValues.TryDecimal(l, out var ln) && WorkflowValues.TryDecimal(r, out var rn))
            {
                return ln.CompareTo(rn);
            }

            var leftText = WorkflowValues.AsString(l);
            var rightText = WorkflowValues.AsString(r);
            if (IsString(l) && IsString(r) && WorkflowTimeText.TryParse(leftText, out var lt) && WorkflowTimeText.TryParse(rightText, out var rt))
            {
                return lt.CompareTo(rt);
            }

            return string.CompareOrdinal(leftText, rightText);
        }

        throw new ExpressionException(WorkflowMessages.ExpressionOperandsNotScalar, MessageArguments.Of(("operator", op)));
    }

    /// <summary>
    /// 要求布尔
    /// </summary>
    private static bool Boolean(JsonNode? node, string op)
        => node is JsonValue value && TryBoolean(value, out var flag) ? flag : throw new ExpressionException(WorkflowMessages.ExpressionOperandsNotBoolean, MessageArguments.Of(("operator", op)));

    /// <summary>
    /// 是不是 JSON 的布尔值
    /// </summary>
    /// <remarks>
    /// 字符串 <c>"true"</c> 不是布尔
    /// </remarks>
    private static bool TryBoolean(JsonValue value, out bool flag)
    {
        var kind = value.GetValueKind();
        flag = kind == JsonValueKind.True;
        return kind is JsonValueKind.True or JsonValueKind.False;
    }

    /// <summary>
    /// 要求数字（字符串形式的数字也接受）
    /// </summary>
    private static decimal Number(JsonNode? node, string op)
        => node is JsonValue value && WorkflowValues.TryDecimal(value, out var number) ? number : throw new ExpressionException(WorkflowMessages.ExpressionOperandsNotNumeric, MessageArguments.Of(("operator", op)));

    /// <summary>
    /// 是否字符串标量
    /// </summary>
    /// <remarks>
    /// 按 JSON 的类型判断：由 <see cref="DateTimeOffset"/>、<see cref="Guid"/> 等构造的值写成 JSON 是字符串，这里也算字符串
    /// </remarks>
    private static bool IsString(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String;

    /// <summary>
    /// 进入一层嵌套
    /// </summary>
    /// <remarks>
    /// 解析是递归下降，每层嵌套要走完整条优先级链。不限层数时持流程定义权限的用户写几千层括号就能撑爆线程栈，栈溢出接不住，整个进程退出。调用方解析完这一层把 <see cref="_depth"/> 减回去；抛出时整个编译结束，不用恢复
    /// </remarks>
    private void EnterNesting()
    {
        if (++_depth > MaxDepth)
        {
            throw new ExpressionException(WorkflowMessages.ExpressionTooDeep, MessageArguments.Of(("max", MaxDepth)));
        }
    }

    /// <summary>
    /// 某个位置上的字符
    /// </summary>
    /// <remarks>
    /// 基本多文种平面以外的字符占两个码元，错误里带出整个字符，不留下半个代理对
    /// </remarks>
    private string CharacterAt(int position)
        => char.IsHighSurrogate(_text[position]) && position + 1 < _text.Length && char.IsLowSurrogate(_text[position + 1])
            ? _text.Substring(position, 2)
            : _text[position].ToString();

    /// <summary>
    /// 跳过空白
    /// </summary>
    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    /// 若当前位置是 <paramref name="token"/> 则消费
    /// </summary>
    /// <remarks>
    /// 运算符前先跳空白
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
    /// 读标识符（字母、数字、下划线，首字符非数字）
    /// </summary>
    /// <remarks>
    /// 不是标识符返回 <see langword="null"/>
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
    /// 读数字（十进制，可带小数）
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
            : throw new ExpressionException(WorkflowMessages.ExpressionNotNumber, MessageArguments.Of(("value", text)));
    }

    /// <summary>
    /// 读引号字符串，支持 <c>\n \t \r \\ \' \"</c> 转义
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

        throw new ExpressionException(WorkflowMessages.ExpressionUnterminatedString);
    }
}
