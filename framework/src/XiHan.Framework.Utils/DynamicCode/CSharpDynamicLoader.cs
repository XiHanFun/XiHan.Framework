#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CSharpDynamicLoader
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2024-07-14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace XiHan.Framework.Utils.DynamicCode;

/// <summary>
/// C#动态代码加载和执行工具类
/// </summary>
public static class CSharpDynamicLoader
{
    /// <summary>
    /// 编译并执行C#代码
    /// </summary>
    /// <param name="sourceCode">C#源代码</param>
    /// <param name="methodName">要执行的方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>方法执行结果</returns>
    public static object? ExecuteCode(string sourceCode, string methodName, params object[] args)
    {
        var assembly = CompileCode(sourceCode);
        return InvokeMethod(assembly, methodName, args);
    }

    /// <summary>
    /// 编译并执行C#代码
    /// </summary>
    /// <param name="sourceCode">C#源代码</param>
    /// <param name="className">类名</param>
    /// <param name="methodName">要执行的方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>方法执行结果</returns>
    public static object? ExecuteCode(string sourceCode, string className, string methodName, params object[] args)
    {
        var assembly = CompileCode(sourceCode);
        return InvokeMethod(assembly, className, methodName, args);
    }

    /// <summary>
    /// 编译C#代码
    /// </summary>
    /// <param name="sourceCode">C#源代码</param>
    /// <returns>编译后的程序集</returns>
    /// <exception cref="InvalidOperationException">编译失败时抛出</exception>
    public static Assembly CompileCode(string sourceCode)
    {
        // 创建语法树
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 确保引用了必要的程序集
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CompilationRelaxationsAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        // 添加当前应用程序域中加载的程序集
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            catch (Exception)
            {
                // 忽略无法加载的程序集
            }
        }

        // 编译选项
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: true);

        // 创建编译
        var assemblyName = "DynamicCodeAssembly_" + Guid.NewGuid().ToString("N");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            compilationOptions);

        // 编译到内存流
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            // 编译错误处理
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();

            throw new InvalidOperationException(
                "编译错误: " + string.Join(Environment.NewLine, errors));
        }

        // 重置流位置并加载程序集
        ms.Seek(0, SeekOrigin.Begin);
        var assemblyLoad = AssemblyLoadContext.Default.LoadFromStream(ms);

        return assemblyLoad;
    }

    /// <summary>
    /// 调用程序集中的方法
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="methodName">方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>方法执行结果</returns>
    public static object? InvokeMethod(Assembly assembly, string methodName, params object[] args)
    {
        // 查找第一个公共静态方法
        foreach (var type in assembly.GetTypes())
        {
            var method = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

            if (method != null)
            {
                return method.Invoke(null, args);
            }
        }

        throw new MissingMethodException($"未找到方法: {methodName}");
    }

    /// <summary>
    /// 调用程序集中特定类型的方法
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="className">类名</param>
    /// <param name="methodName">方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>方法执行结果</returns>
    public static object? InvokeMethod(Assembly assembly, string className, string methodName, params object[] args)
    {
        // 查找指定的类型
        var type = assembly.GetType(className)
            ?? throw new TypeLoadException($"未找到类型: {className}");

        // 查找方法
        var method = type.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException($"未找到方法: {className}.{methodName}");

        // 如果是静态方法，直接调用
        if (method.IsStatic)
        {
            return method.Invoke(null, args);
        }

        // 如果是实例方法，创建类型实例并调用
        var instance = Activator.CreateInstance(type);
        return method.Invoke(instance, args);
    }

    /// <summary>
    /// 从文件加载C#代码并执行
    /// </summary>
    /// <param name="filePath">C#源代码文件路径</param>
    /// <param name="methodName">要执行的方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>方法执行结果</returns>
    public static object? ExecuteFromFile(string filePath, string methodName, params object[] args)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到源代码文件", filePath);
        }

        var sourceCode = File.ReadAllText(filePath);
        return ExecuteCode(sourceCode, methodName, args);
    }
}
