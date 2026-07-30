// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Expressions;

/// <summary>
/// 表达式求值上下文
/// </summary>
internal sealed class ExpressionEvaluationContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="variables">变量上下文</param>
    /// <param name="nowProvider">当前时间提供者</param>
    public ExpressionEvaluationContext(IReadOnlyDictionary<string, object?> variables, Func<DateTime> nowProvider)
    {
        Variables = variables;
        NowProvider = nowProvider;
    }

    /// <summary>
    /// 变量上下文
    /// </summary>
    public IReadOnlyDictionary<string, object?> Variables { get; }

    /// <summary>
    /// 当前时间提供者
    /// </summary>
    public Func<DateTime> NowProvider { get; }
}

/// <summary>
/// 表达式语法树节点
/// </summary>
internal abstract class ExpressionNode
{
    /// <summary>
    /// 求值
    /// </summary>
    /// <param name="context">求值上下文</param>
    /// <returns>求值结果</returns>
    public abstract object? Evaluate(ExpressionEvaluationContext context);
}

/// <summary>
/// 字面量节点
/// </summary>
internal sealed class LiteralNode : ExpressionNode
{
    private readonly object? _value;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">字面量值</param>
    public LiteralNode(object? value)
    {
        _value = value;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        return _value;
    }
}

/// <summary>
/// 变量引用节点
/// </summary>
internal sealed class VariableNode : ExpressionNode
{
    private readonly string _name;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">变量名</param>
    public VariableNode(string name)
    {
        _name = name;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        return context.Variables.TryGetValue(_name, out var value)
            ? WorkflowValueConverter.Normalize(value)
            : throw new WorkflowException($"表达式引用了不存在的变量 {_name}");
    }
}

/// <summary>
/// 成员访问节点（点号导航）
/// </summary>
internal sealed class MemberAccessNode : ExpressionNode
{
    private readonly ExpressionNode _target;
    private readonly string _memberName;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="target">目标节点</param>
    /// <param name="memberName">成员名</param>
    public MemberAccessNode(ExpressionNode target, string memberName)
    {
        _target = target;
        _memberName = memberName;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        var target = _target.Evaluate(context);
        return target switch
        {
            null => null,
            IReadOnlyDictionary<string, object?> readOnlyDictionary =>
                readOnlyDictionary.TryGetValue(_memberName, out var value) ? WorkflowValueConverter.Normalize(value) : null,
            IDictionary<string, object?> dictionary =>
                dictionary.TryGetValue(_memberName, out var value) ? WorkflowValueConverter.Normalize(value) : null,
            _ => EvaluateByReflection(target)
        };
    }

    private object? EvaluateByReflection(object target)
    {
        var property = target.GetType().GetProperty(_memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property is null
            ? throw new WorkflowException($"表达式访问了类型 {target.GetType().Name} 上不存在的成员 {_memberName}")
            : WorkflowValueConverter.Normalize(property.GetValue(target));
    }
}

/// <summary>
/// 索引访问节点
/// </summary>
internal sealed class IndexAccessNode : ExpressionNode
{
    private readonly ExpressionNode _target;
    private readonly ExpressionNode _index;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="target">目标节点</param>
    /// <param name="index">索引节点</param>
    public IndexAccessNode(ExpressionNode target, ExpressionNode index)
    {
        _target = target;
        _index = index;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        var target = _target.Evaluate(context);
        var index = _index.Evaluate(context);

        switch (target)
        {
            case null:
                return null;

            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
            {
                var key = ExpressionOperations.ToDisplayString(index);
                return readOnlyDictionary.TryGetValue(key, out var value) ? WorkflowValueConverter.Normalize(value) : null;
            }

            case IDictionary<string, object?> dictionary:
            {
                var key = ExpressionOperations.ToDisplayString(index);
                return dictionary.TryGetValue(key, out var value) ? WorkflowValueConverter.Normalize(value) : null;
            }

            case System.Collections.IList list:
            {
                var position = (int)ExpressionOperations.ToNumber(index, "索引");
                return position < 0 || position >= list.Count
                    ? throw new WorkflowException($"表达式索引 {position} 超出集合范围（0..{list.Count - 1}）")
                    : WorkflowValueConverter.Normalize(list[position]);
            }

            default:
                throw new WorkflowException($"表达式对类型 {target.GetType().Name} 使用了不支持的索引访问");
        }
    }
}

/// <summary>
/// 一元运算节点
/// </summary>
internal sealed class UnaryNode : ExpressionNode
{
    private readonly string _operator;
    private readonly ExpressionNode _operand;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="operator">运算符</param>
    /// <param name="operand">操作数节点</param>
    public UnaryNode(string @operator, ExpressionNode operand)
    {
        _operator = @operator;
        _operand = operand;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        var operand = _operand.Evaluate(context);
        return _operator switch
        {
            "!" => operand is bool boolean
                ? !boolean
                : throw new WorkflowException("逻辑非运算符只能作用于布尔值"),
            "-" => -ExpressionOperations.ToNumber(operand, "负号"),
            _ => throw new WorkflowException($"不支持的一元运算符 {_operator}")
        };
    }
}

/// <summary>
/// 二元运算节点
/// </summary>
internal sealed class BinaryNode : ExpressionNode
{
    private readonly string _operator;
    private readonly ExpressionNode _left;
    private readonly ExpressionNode _right;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="operator">运算符</param>
    /// <param name="left">左操作数节点</param>
    /// <param name="right">右操作数节点</param>
    public BinaryNode(string @operator, ExpressionNode left, ExpressionNode right)
    {
        _operator = @operator;
        _left = left;
        _right = right;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        // 逻辑运算短路求值
        if (_operator is "&&" or "||")
        {
            var leftBoolean = _left.Evaluate(context) is bool leftValue
                ? leftValue
                : throw new WorkflowException($"逻辑运算符 {_operator} 的左操作数必须为布尔值");

            if (_operator == "&&" && !leftBoolean)
            {
                return false;
            }

            if (_operator == "||" && leftBoolean)
            {
                return true;
            }

            return _right.Evaluate(context) is bool rightValue
                ? rightValue
                : throw new WorkflowException($"逻辑运算符 {_operator} 的右操作数必须为布尔值");
        }

        var left = _left.Evaluate(context);
        var right = _right.Evaluate(context);

        return _operator switch
        {
            "==" => ExpressionOperations.LooseEquals(left, right),
            "!=" => !ExpressionOperations.LooseEquals(left, right),
            "<" => ExpressionOperations.Compare(left, right) < 0,
            "<=" => ExpressionOperations.Compare(left, right) <= 0,
            ">" => ExpressionOperations.Compare(left, right) > 0,
            ">=" => ExpressionOperations.Compare(left, right) >= 0,
            "+" => left is string || right is string
                ? ExpressionOperations.ToDisplayString(left) + ExpressionOperations.ToDisplayString(right)
                : ExpressionOperations.ToNumber(left, "加法") + ExpressionOperations.ToNumber(right, "加法"),
            "-" => ExpressionOperations.ToNumber(left, "减法") - ExpressionOperations.ToNumber(right, "减法"),
            "*" => ExpressionOperations.ToNumber(left, "乘法") * ExpressionOperations.ToNumber(right, "乘法"),
            "/" => ExpressionOperations.ToNumber(left, "除法") / ExpressionOperations.ToNumber(right, "除法"),
            "%" => ExpressionOperations.ToNumber(left, "取余") % ExpressionOperations.ToNumber(right, "取余"),
            _ => throw new WorkflowException($"不支持的二元运算符 {_operator}")
        };
    }
}

/// <summary>
/// 函数调用节点
/// </summary>
internal sealed class FunctionCallNode : ExpressionNode
{
    private readonly string _name;
    private readonly List<ExpressionNode> _arguments;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="arguments">实参节点列表</param>
    public FunctionCallNode(string name, List<ExpressionNode> arguments)
    {
        _name = name;
        _arguments = arguments;
    }

    /// <inheritdoc />
    public override object? Evaluate(ExpressionEvaluationContext context)
    {
        var arguments = _arguments.Select(argument => argument.Evaluate(context)).ToArray();
        return ExpressionOperations.InvokeFunction(_name, arguments, context);
    }
}

/// <summary>
/// 表达式运算语义（类型强制、比较、内置函数）
/// </summary>
internal static class ExpressionOperations
{
    /// <summary>
    /// 强制转换为数值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="operation">运算描述（用于错误提示）</param>
    /// <returns>数值</returns>
    /// <exception cref="WorkflowException">无法转换时抛出</exception>
    public static decimal ToNumber(object? value, string operation)
    {
        return value switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            double doubleValue => (decimal)doubleValue,
            float floatValue => (decimal)floatValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new WorkflowException($"{operation}运算要求数值操作数，实际为 {DescribeType(value)}")
        };
    }

    /// <summary>
    /// 宽松相等比较（数值按值比较、字符串按序比较、null 安全）
    /// </summary>
    /// <param name="left">左值</param>
    /// <param name="right">右值</param>
    /// <returns>相等返回 true</returns>
    public static bool LooseEquals(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return ToNumber(left, "相等比较") == ToNumber(right, "相等比较");
        }

        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean == rightBoolean;
        }

        if (left is string leftText && right is string rightText)
        {
            return string.Equals(leftText, rightText, StringComparison.Ordinal);
        }

        // 与大小比较保持一致：时间与时间字符串可互比（持久化往返后 DateTime 会变成 ISO 字符串）
        if ((left is DateTime || right is DateTime)
            && TryToDateTime(left, out var leftTime) && TryToDateTime(right, out var rightTime))
        {
            return leftTime == rightTime;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// 大小比较（数值/字符串/时间）
    /// </summary>
    /// <param name="left">左值</param>
    /// <param name="right">右值</param>
    /// <returns>比较结果（负数小于、零相等、正数大于）</returns>
    /// <exception cref="WorkflowException">类型不可比较时抛出</exception>
    public static int Compare(object? left, object? right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return ToNumber(left, "大小比较").CompareTo(ToNumber(right, "大小比较"));
        }

        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }

        if (TryToDateTime(left, out var leftTime) && TryToDateTime(right, out var rightTime))
        {
            return leftTime.CompareTo(rightTime);
        }

        throw new WorkflowException($"无法比较 {DescribeType(left)} 与 {DescribeType(right)} 的大小");
    }

    /// <summary>
    /// 转换为显示字符串（模板插值与字符串拼接使用）
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>显示字符串（null 为空串）</returns>
    public static string ToDisplayString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    /// <summary>
    /// 调用内置函数
    /// </summary>
    /// <param name="name">函数名（不区分大小写）</param>
    /// <param name="arguments">实参</param>
    /// <param name="context">求值上下文</param>
    /// <returns>函数结果</returns>
    /// <exception cref="WorkflowException">未知函数或参数不合法时抛出</exception>
    public static object? InvokeFunction(string name, object?[] arguments, ExpressionEvaluationContext context)
    {
        switch (name.ToLowerInvariant())
        {
            case "len":
                RequireArgumentCount(name, arguments, 1);
                return arguments[0] switch
                {
                    null => 0m,
                    string text => (decimal)text.Length,
                    System.Collections.ICollection collection => (decimal)collection.Count,
                    System.Collections.IEnumerable enumerable => (decimal)enumerable.Cast<object?>().Count(),
                    _ => throw new WorkflowException("len 函数要求字符串或集合参数")
                };

            case "contains":
                RequireArgumentCount(name, arguments, 2);
                return arguments[0] switch
                {
                    null => false,
                    string text => text.Contains(ToDisplayString(arguments[1]), StringComparison.Ordinal),
                    System.Collections.IEnumerable enumerable => enumerable.Cast<object?>()
                        .Any(item => LooseEquals(WorkflowValueConverter.Normalize(item), arguments[1])),
                    _ => throw new WorkflowException("contains 函数要求字符串或集合参数")
                };

            case "startswith":
                RequireArgumentCount(name, arguments, 2);
                return ToDisplayString(arguments[0]).StartsWith(ToDisplayString(arguments[1]), StringComparison.Ordinal);

            case "endswith":
                RequireArgumentCount(name, arguments, 2);
                return ToDisplayString(arguments[0]).EndsWith(ToDisplayString(arguments[1]), StringComparison.Ordinal);

            case "upper":
                RequireArgumentCount(name, arguments, 1);
                return ToDisplayString(arguments[0]).ToUpperInvariant();

            case "lower":
                RequireArgumentCount(name, arguments, 1);
                return ToDisplayString(arguments[0]).ToLowerInvariant();

            case "trim":
                RequireArgumentCount(name, arguments, 1);
                return ToDisplayString(arguments[0]).Trim();

            case "isnullorempty":
                RequireArgumentCount(name, arguments, 1);
                return arguments[0] switch
                {
                    null => true,
                    string text => text.Length == 0,
                    System.Collections.ICollection collection => collection.Count == 0,
                    _ => false
                };

            case "abs":
                RequireArgumentCount(name, arguments, 1);
                return Math.Abs(ToNumber(arguments[0], "abs"));

            case "min":
                RequireArgumentCount(name, arguments, 2);
                return Math.Min(ToNumber(arguments[0], "min"), ToNumber(arguments[1], "min"));

            case "max":
                RequireArgumentCount(name, arguments, 2);
                return Math.Max(ToNumber(arguments[0], "max"), ToNumber(arguments[1], "max"));

            case "round":
                return arguments.Length switch
                {
                    1 => Math.Round(ToNumber(arguments[0], "round"), MidpointRounding.AwayFromZero),
                    2 => Math.Round(ToNumber(arguments[0], "round"), (int)ToNumber(arguments[1], "round"), MidpointRounding.AwayFromZero),
                    _ => throw new WorkflowException("round 函数要求 1 或 2 个参数")
                };

            case "tonumber":
                RequireArgumentCount(name, arguments, 1);
                return ToNumber(arguments[0], "toNumber");

            case "tostring":
                RequireArgumentCount(name, arguments, 1);
                return ToDisplayString(arguments[0]);

            case "now":
                RequireArgumentCount(name, arguments, 0);
                return context.NowProvider();

            case "date":
                RequireArgumentCount(name, arguments, 1);
                return DateTime.Parse(ToDisplayString(arguments[0]), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            default:
                throw new WorkflowException($"未知的表达式函数 {name}");
        }
    }

    private static void RequireArgumentCount(string name, object?[] arguments, int expected)
    {
        if (arguments.Length != expected)
        {
            throw new WorkflowException($"{name} 函数要求 {expected} 个参数，实际为 {arguments.Length} 个");
        }
    }

    private static bool IsNumeric(object? value)
    {
        return value is decimal or int or long or double or float or short or byte;
    }

    private static bool TryToDateTime(object? value, out DateTime result)
    {
        switch (value)
        {
            case DateTime dateTime:
                result = dateTime;
                return true;

            case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed):
                result = parsed;
                return true;

            default:
                result = default;
                return false;
        }
    }

    private static string DescribeType(object? value)
    {
        return value is null ? "null" : value.GetType().Name;
    }
}

/// <summary>
/// 表达式解析器（递归下降）
/// </summary>
/// <remarks>
/// 语法：逻辑或 → 逻辑与 → 相等 → 大小 → 加减 → 乘除余 → 一元 → 基元（字面量/变量/函数/括号/点号导航/索引）。
/// 字面量：数字、单双引号字符串、true/false/null。
/// </remarks>
internal static class ExpressionParser
{
    /// <summary>
    /// 解析表达式（语法非法时抛出工作流异常）
    /// </summary>
    /// <param name="expression">表达式文本</param>
    /// <returns>语法树根节点</returns>
    public static ExpressionNode Parse(string expression)
    {
        var tokens = Tokenize(expression);
        var position = 0;
        var node = ParseOr(tokens, ref position);

        return tokens[position].Type != TokenType.End
            ? throw new WorkflowException($"表达式存在无法解析的剩余内容：{expression}")
            : node;
    }

    private enum TokenType
    {
        Number,
        String,
        Identifier,
        Operator,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Dot,
        Comma,
        End
    }

    private readonly record struct Token(TokenType Type, string Text, decimal Number);

    private static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < expression.Length)
        {
            var current = expression[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = index;
                while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                {
                    index++;
                }

                var text = expression[start..index];
                tokens.Add(!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    ? throw new WorkflowException($"表达式包含非法数字字面量 {text}")
                    : new Token(TokenType.Number, text, number));
                continue;
            }

            if (current is '\'' or '"')
            {
                var quote = current;
                index++;
                var builder = new System.Text.StringBuilder();
                var closed = false;

                while (index < expression.Length)
                {
                    var character = expression[index];
                    if (character == '\\' && index + 1 < expression.Length)
                    {
                        var escaped = expression[index + 1];
                        builder.Append(escaped switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            'r' => '\r',
                            _ => escaped
                        });
                        index += 2;
                        continue;
                    }

                    if (character == quote)
                    {
                        closed = true;
                        index++;
                        break;
                    }

                    builder.Append(character);
                    index++;
                }

                if (!closed)
                {
                    throw new WorkflowException("表达式字符串字面量未闭合");
                }

                tokens.Add(new Token(TokenType.String, builder.ToString(), 0));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index;
                while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
                {
                    index++;
                }

                tokens.Add(new Token(TokenType.Identifier, expression[start..index], 0));
                continue;
            }

            switch (current)
            {
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "(", 0));
                    index++;
                    continue;

                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")", 0));
                    index++;
                    continue;

                case '[':
                    tokens.Add(new Token(TokenType.LeftBracket, "[", 0));
                    index++;
                    continue;

                case ']':
                    tokens.Add(new Token(TokenType.RightBracket, "]", 0));
                    index++;
                    continue;

                case '.':
                    tokens.Add(new Token(TokenType.Dot, ".", 0));
                    index++;
                    continue;

                case ',':
                    tokens.Add(new Token(TokenType.Comma, ",", 0));
                    index++;
                    continue;
            }

            // 双字符运算符优先
            if (index + 1 < expression.Length)
            {
                var pair = expression.Substring(index, 2);
                if (pair is "==" or "!=" or "<=" or ">=" or "&&" or "||")
                {
                    tokens.Add(new Token(TokenType.Operator, pair, 0));
                    index += 2;
                    continue;
                }
            }

            if (current is '+' or '-' or '*' or '/' or '%' or '<' or '>' or '!')
            {
                tokens.Add(new Token(TokenType.Operator, current.ToString(), 0));
                index++;
                continue;
            }

            throw new WorkflowException($"表达式包含无法识别的字符 '{current}'");
        }

        tokens.Add(new Token(TokenType.End, string.Empty, 0));
        return tokens;
    }

    private static ExpressionNode ParseOr(List<Token> tokens, ref int position)
    {
        var left = ParseAnd(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "||" })
        {
            position++;
            left = new BinaryNode("||", left, ParseAnd(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseAnd(List<Token> tokens, ref int position)
    {
        var left = ParseEquality(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "&&" })
        {
            position++;
            left = new BinaryNode("&&", left, ParseEquality(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseEquality(List<Token> tokens, ref int position)
    {
        var left = ParseRelational(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "==" or "!=" })
        {
            var op = tokens[position].Text;
            position++;
            left = new BinaryNode(op, left, ParseRelational(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseRelational(List<Token> tokens, ref int position)
    {
        var left = ParseAdditive(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "<" or "<=" or ">" or ">=" })
        {
            var op = tokens[position].Text;
            position++;
            left = new BinaryNode(op, left, ParseAdditive(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseAdditive(List<Token> tokens, ref int position)
    {
        var left = ParseMultiplicative(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "+" or "-" })
        {
            var op = tokens[position].Text;
            position++;
            left = new BinaryNode(op, left, ParseMultiplicative(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseMultiplicative(List<Token> tokens, ref int position)
    {
        var left = ParseUnary(tokens, ref position);
        while (tokens[position] is { Type: TokenType.Operator, Text: "*" or "/" or "%" })
        {
            var op = tokens[position].Text;
            position++;
            left = new BinaryNode(op, left, ParseUnary(tokens, ref position));
        }

        return left;
    }

    private static ExpressionNode ParseUnary(List<Token> tokens, ref int position)
    {
        if (tokens[position] is { Type: TokenType.Operator, Text: "!" or "-" })
        {
            var op = tokens[position].Text;
            position++;
            return new UnaryNode(op, ParseUnary(tokens, ref position));
        }

        return ParsePostfix(tokens, ref position);
    }

    private static ExpressionNode ParsePostfix(List<Token> tokens, ref int position)
    {
        var node = ParsePrimary(tokens, ref position);

        while (true)
        {
            if (tokens[position].Type == TokenType.Dot)
            {
                position++;
                if (tokens[position].Type != TokenType.Identifier)
                {
                    throw new WorkflowException("表达式点号后必须为成员名");
                }

                node = new MemberAccessNode(node, tokens[position].Text);
                position++;
                continue;
            }

            if (tokens[position].Type == TokenType.LeftBracket)
            {
                position++;
                var index = ParseOr(tokens, ref position);
                Expect(tokens, ref position, TokenType.RightBracket, "]");
                node = new IndexAccessNode(node, index);
                continue;
            }

            return node;
        }
    }

    private static ExpressionNode ParsePrimary(List<Token> tokens, ref int position)
    {
        var token = tokens[position];

        switch (token.Type)
        {
            case TokenType.Number:
                position++;
                return new LiteralNode(token.Number);

            case TokenType.String:
                position++;
                return new LiteralNode(token.Text);

            case TokenType.Identifier:
                switch (token.Text)
                {
                    case "true":
                        position++;
                        return new LiteralNode(true);

                    case "false":
                        position++;
                        return new LiteralNode(false);

                    case "null":
                        position++;
                        return new LiteralNode(null);
                }

                // 函数调用
                if (tokens[position + 1].Type == TokenType.LeftParen)
                {
                    var name = token.Text;
                    position += 2;
                    var arguments = new List<ExpressionNode>();

                    if (tokens[position].Type != TokenType.RightParen)
                    {
                        arguments.Add(ParseOr(tokens, ref position));
                        while (tokens[position].Type == TokenType.Comma)
                        {
                            position++;
                            arguments.Add(ParseOr(tokens, ref position));
                        }
                    }

                    Expect(tokens, ref position, TokenType.RightParen, ")");
                    return new FunctionCallNode(name, arguments);
                }

                position++;
                return new VariableNode(token.Text);

            case TokenType.LeftParen:
            {
                position++;
                var node = ParseOr(tokens, ref position);
                Expect(tokens, ref position, TokenType.RightParen, ")");
                return node;
            }

            default:
                throw new WorkflowException($"表达式存在非法语法（意外的 {token.Text}）");
        }
    }

    private static void Expect(List<Token> tokens, ref int position, TokenType type, string display)
    {
        if (tokens[position].Type != type)
        {
            throw new WorkflowException($"表达式缺少 {display}");
        }

        position++;
    }
}
