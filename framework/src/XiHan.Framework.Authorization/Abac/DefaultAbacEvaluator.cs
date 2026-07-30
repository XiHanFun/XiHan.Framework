// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Globalization;

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// 默认 ABAC 评估器
/// </summary>
public class DefaultAbacEvaluator : IAbacEvaluator
{
    /// <inheritdoc />
    public Task<AbacEvaluationResult> EvaluateAsync(AbacEvaluationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var policyCode = context.PolicyCode?.Trim();
        if (string.IsNullOrWhiteSpace(policyCode))
        {
            return Task.FromResult(AbacEvaluationResult.Allow("未配置 ABAC 策略"));
        }

        if (string.Equals(policyCode, "allow", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AbacEvaluationResult.Allow("命中 allow 策略"));
        }

        if (IsSameTenantPolicy(policyCode))
        {
            var tenantAllowed = MatchSubjectToAnyResourceValue(
                context,
                "tenant_id",
                "tenant_id",
                "route.tenant_id",
                "query.tenant_id");
            return Task.FromResult(tenantAllowed
                ? AbacEvaluationResult.Allow("租户匹配")
                : AbacEvaluationResult.Deny("租户不匹配"));
        }

        if (IsSelfOnlyPolicy(policyCode))
        {
            var selfAllowed = MatchSubjectToAnyResourceValue(
                context,
                "user_id",
                "user_id",
                "owner_user_id",
                "route.user_id",
                "query.user_id");
            return Task.FromResult(selfAllowed
                ? AbacEvaluationResult.Allow("用户归属匹配")
                : AbacEvaluationResult.Deny("用户归属不匹配"));
        }

        if (TryEvaluateComparisonPolicy(policyCode, context, out var comparisonAllowed))
        {
            return Task.FromResult(comparisonAllowed
                ? AbacEvaluationResult.Allow("表达式评估通过")
                : AbacEvaluationResult.Deny("表达式评估未通过"));
        }

        return Task.FromResult(AbacEvaluationResult.Deny($"不支持的 ABAC 策略: {policyCode}"));
    }

    private static bool IsSameTenantPolicy(string policyCode)
    {
        return string.Equals(policyCode, "same_tenant", StringComparison.OrdinalIgnoreCase)
               || string.Equals(policyCode, "tenant_match", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelfOnlyPolicy(string policyCode)
    {
        return string.Equals(policyCode, "self_only", StringComparison.OrdinalIgnoreCase)
               || string.Equals(policyCode, "owner_match", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchSubjectToAnyResourceValue(
        AbacEvaluationContext context,
        string subjectKey,
        params string[] resourceKeys)
    {
        if (!context.SubjectAttributes.TryGetValue(subjectKey, out var subjectValue))
        {
            return false;
        }

        foreach (var resourceKey in resourceKeys)
        {
            if (context.ResourceAttributes.TryGetValue(resourceKey, out var resourceValue))
            {
                if (AreValuesEqual(subjectValue, resourceValue))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryEvaluateComparisonPolicy(string policyCode, AbacEvaluationContext context, out bool allowed)
    {
        allowed = false;

        var @operator = ResolveOperator(policyCode);
        if (string.IsNullOrWhiteSpace(@operator))
        {
            return false;
        }

        var operatorIndex = policyCode.IndexOf(@operator, StringComparison.Ordinal);
        if (operatorIndex <= 0 || operatorIndex >= policyCode.Length - @operator.Length)
        {
            return false;
        }

        var leftOperand = policyCode[..operatorIndex].Trim();
        var rightOperand = policyCode[(operatorIndex + @operator.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(leftOperand) || string.IsNullOrWhiteSpace(rightOperand))
        {
            return false;
        }

        if (!TryResolveOperand(leftOperand, context, out var leftValue)
            || !TryResolveOperand(rightOperand, context, out var rightValue))
        {
            return false;
        }

        var equals = AreValuesEqual(leftValue, rightValue);
        allowed = @operator == "!=" ? !equals : equals;
        return true;
    }

    private static string ResolveOperator(string policyCode)
    {
        if (policyCode.Contains("!=", StringComparison.Ordinal))
        {
            return "!=";
        }

        if (policyCode.Contains("==", StringComparison.Ordinal))
        {
            return "==";
        }

        if (policyCode.Contains('='))
        {
            return "=";
        }

        return string.Empty;
    }

    private static bool TryResolveOperand(string operand, AbacEvaluationContext context, out object? value)
    {
        value = null;

        if (TryResolveContextValue("subject.", operand, context.SubjectAttributes, out value))
        {
            return true;
        }

        if (TryResolveContextValue("resource.", operand, context.ResourceAttributes, out value))
        {
            return true;
        }

        if (TryResolveContextValue("environment.", operand, context.EnvironmentAttributes, out value))
        {
            return true;
        }

        if (TryResolveLiteral(operand, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveContextValue(
        string prefix,
        string operand,
        IReadOnlyDictionary<string, object?> source,
        out object? value)
    {
        value = null;
        if (!operand.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var key = operand[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!source.TryGetValue(key, out value))
        {
            return false;
        }

        return true;
    }

    private static bool TryResolveLiteral(string operand, out object? value)
    {
        value = null;

        if (operand.Length >= 2
            && ((operand[0] == '"' && operand[^1] == '"')
                || (operand[0] == '\'' && operand[^1] == '\'')))
        {
            value = operand[1..^1];
            return true;
        }

        if (bool.TryParse(operand, out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (decimal.TryParse(operand, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
        {
            value = numericValue;
            return true;
        }

        value = operand;
        return true;
    }

    private static bool AreValuesEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryMatchCollection(left, right) || TryMatchCollection(right, left))
        {
            return true;
        }

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal == rightDecimal;
        }

        return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMatchCollection(object source, object target)
    {
        if (source is string || source is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var item in enumerable)
        {
            if (AreValuesEqual(item, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConvertToDecimal(object value, out decimal result)
    {
        if (value is decimal decimalValue)
        {
            result = decimalValue;
            return true;
        }

        return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
