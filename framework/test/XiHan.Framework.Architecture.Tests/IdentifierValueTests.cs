// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 标识值门禁：从字符串的转换是显式的，default 值不冒充合法标识
/// </summary>
/// <remarks>
/// 规则见 RFC-0001「标识值」：会校验的转换不能是隐式的；字符串承载的标识（有 <c>(string)</c> 构造与 <c>string Value</c> 的公开结构）的 <see langword="default"/> 读 <c>Value</c> 抛出 <see cref="InvalidOperationException"/>，<c>ToString</c> 返回空串
/// </remarks>
public sealed class IdentifierValueTests
{
    [Fact]
    public void NoPublicType_ConvertsImplicitlyFromString()
    {
        var failures = new List<string>();
        foreach (var type in PublicTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.Name == "op_Implicit" && method.GetParameters() is [{ ParameterType: var source }] && source == typeof(string))
                {
                    failures.Add($"'{type.FullName}' converts implicitly from 'string'; a validating conversion must be explicit.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void StringBackedIdentifiers_RejectTheirDefaultValue()
    {
        var checkedTypes = new List<string>();
        var failures = new List<string>();
        foreach (var type in PublicTypes())
        {
            if (!type.IsValueType || type.IsEnum || type.ContainsGenericParameters
                || type.GetConstructor([typeof(string)]) is null
                || type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) is not { PropertyType: var valueType } value
                || valueType != typeof(string))
            {
                continue;
            }

            checkedTypes.Add(type.Name);
            var instance = Activator.CreateInstance(type)!;
            try
            {
                value.GetValue(instance);
                failures.Add($"'{type.FullName}': reading 'Value' of the default value must throw 'InvalidOperationException'.");
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                // 预期：default 没有文本
            }

            if (instance.ToString() != string.Empty)
            {
                failures.Add($"'{type.FullName}': 'ToString' of the default value must return an empty string.");
            }
        }

        Assert.Contains("CacheKey", checkedTypes);
        Assert.Contains("JobName", checkedTypes);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 全部正式包的公开类型
    /// </summary>
    private static IEnumerable<Type> PublicTypes() => NamingTests.LoadSourceAssemblies().SelectMany(static assembly => assembly.GetExportedTypes());
}
