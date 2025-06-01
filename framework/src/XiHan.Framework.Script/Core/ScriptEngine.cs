#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptEngine
// Guid:a3986aeb-032e-42a0-b75b-435a0a055810
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:06:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Exceptions;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 脚本引擎实现
/// 基于 Roslyn 编译器的动态脚本执行引擎
/// </summary>
public class ScriptEngine : IScriptEngine, IDisposable
{
    private readonly ConcurrentDictionary<string, CompilationResult> _compilationCache = new();
    private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new();
    private readonly EngineStatistics _statistics = new() { StartTime = DateTime.Now };
    private readonly Lock _lockObject = new();
    private bool _disposed = false;

    /// <summary>
    /// 执行脚本代码
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public async Task<ScriptResult> ExecuteAsync(string scriptCode, ScriptOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(scriptCode))
        {
            return ScriptResult.Failure("脚本代码不能为空");
        }

        options ??= ScriptOptions.Default;
        var stopwatch = Stopwatch.StartNew();
        var memoryUsage = MemoryUsage.Create();

        try
        {
            // 获取缓存键
            var cacheKey = options.CacheKey ?? GenerateCacheKey(scriptCode, options);

            ScriptResult result;
            if (options.EnableCache && _compilationCache.TryGetValue(cacheKey, out var cachedCompilation))
            {
                // 从缓存执行
                _statistics.CacheHits++;
                result = await ExecuteCompiledAsync(cachedCompilation, options);
                result.FromCache = true;
                result.CacheKey = cacheKey;
            }
            else
            {
                // 编译并执行
                _statistics.CacheMisses++;
                var compilationResult = await CompileInternalAsync(scriptCode, options);

                if (!compilationResult.IsSuccess)
                {
                    return ScriptResult.CompilationFailure(compilationResult.Diagnostics);
                }

                // 缓存编译结果
                if (options.EnableCache)
                {
                    _compilationCache.TryAdd(cacheKey, compilationResult);
                }

                result = await ExecuteCompiledAsync(compilationResult, options);
                result.CacheKey = cacheKey;
                result.CompilationTimeMs = compilationResult.CompilationTimeMs;
            }

            stopwatch.Stop();
            memoryUsage.Complete();

            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.MemoryUsage = memoryUsage;

            // 更新统计信息
            lock (_lockObject)
            {
                _statistics.TotalExecutions++;
                if (result.IsSuccess)
                {
                    _statistics.SuccessfulExecutions++;
                }
                else
                {
                    _statistics.FailedExecutions++;
                }

                _statistics.AverageExecutionTimeMs = UpdateAverage(_statistics.AverageExecutionTimeMs,
                    result.ExecutionTimeMs, _statistics.TotalExecutions);

                if (result.CompilationTimeMs > 0)
                {
                    _statistics.AverageCompilationTimeMs = UpdateAverage(_statistics.AverageCompilationTimeMs,
                        result.CompilationTimeMs, _statistics.TotalExecutions);
                }

                _statistics.CacheSize = _compilationCache.Count;
                _statistics.TotalMemoryUsage += memoryUsage.MemoryIncrease;
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            memoryUsage.Complete();

            var result = ScriptResult.Failure($"脚本执行异常: {ex.Message}", ex);
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.MemoryUsage = memoryUsage;

            lock (_lockObject)
            {
                _statistics.TotalExecutions++;
                _statistics.FailedExecutions++;
            }

            return result;
        }
    }

    /// <summary>
    /// 执行脚本代码并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public async Task<ScriptResult<T>> ExecuteAsync<T>(string scriptCode, ScriptOptions? options = null)
    {
        var result = await ExecuteAsync(scriptCode, options);
        return ScriptResult<T>.FromBase(result);
    }

    /// <summary>
    /// 执行脚本文件
    /// </summary>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public async Task<ScriptResult> ExecuteFileAsync(string scriptFilePath, ScriptOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(scriptFilePath))
        {
            return ScriptResult.Failure("脚本文件路径不能为空");
        }

        if (!File.Exists(scriptFilePath))
        {
            return ScriptResult.Failure($"脚本文件不存在: {scriptFilePath}");
        }

        try
        {
            options ??= ScriptOptions.Default;

            // 安全检查：验证文件扩展名
            ValidateScriptFileSecurity(scriptFilePath, options.SecurityOptions);

            var scriptCode = await File.ReadAllTextAsync(scriptFilePath);

            // 使用文件路径作为缓存键的一部分
            if (string.IsNullOrEmpty(options.CacheKey))
            {
                options = options.WithCacheKey($"file:{scriptFilePath}:{new FileInfo(scriptFilePath).LastWriteTime.Ticks}");
            }

            return await ExecuteAsync(scriptCode, options);
        }
        catch (ScriptSecurityException)
        {
            throw; // 重新抛出安全异常
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ScriptLoadException($"没有权限访问脚本文件: {scriptFilePath}", ex, scriptFilePath);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new ScriptLoadException($"脚本文件目录不存在: {scriptFilePath}", ex, scriptFilePath);
        }
        catch (FileNotFoundException ex)
        {
            throw new ScriptLoadException($"脚本文件未找到: {scriptFilePath}", ex, scriptFilePath);
        }
        catch (IOException ex)
        {
            throw new ScriptLoadException($"读取脚本文件失败: {scriptFilePath} - {ex.Message}", ex, scriptFilePath);
        }
        catch (Exception ex)
        {
            throw new ScriptLoadException($"加载脚本文件时发生未知错误: {scriptFilePath} - {ex.Message}", ex, scriptFilePath);
        }
    }

    /// <summary>
    /// 执行脚本文件并返回指定类型的结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    public async Task<ScriptResult<T>> ExecuteFileAsync<T>(string scriptFilePath, ScriptOptions? options = null)
    {
        var result = await ExecuteFileAsync(scriptFilePath, options);
        return ScriptResult<T>.FromBase(result);
    }

    /// <summary>
    /// 编译脚本代码
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>编译结果</returns>
    public async Task<CompilationResult> CompileAsync(string scriptCode, ScriptOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(scriptCode))
        {
            return CompilationResult.Failure([]);
        }

        options ??= ScriptOptions.Default;
        return await CompileInternalAsync(scriptCode, options);
    }

    /// <summary>
    /// 创建脚本实例
    /// </summary>
    /// <typeparam name="T">实例类型</typeparam>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>脚本实例</returns>
    public async Task<T?> CreateInstanceAsync<T>(string scriptCode, ScriptOptions? options = null) where T : class
    {
        var result = await ExecuteAsync(scriptCode, options);
        return !result.IsSuccess ? null : result.Value as T;
    }

    /// <summary>
    /// 评估表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public async Task<object?> EvaluateAsync(string expression, ScriptOptions? options = null)
    {
        options ??= ScriptOptions.Default;
        options = options.WithScriptType(ScriptType.Expression);

        var result = await ExecuteAsync(expression, options);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// 评估表达式并返回指定类型
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <param name="options">脚本选项</param>
    /// <returns>表达式结果</returns>
    public async Task<T?> EvaluateAsync<T>(string expression, ScriptOptions? options = null)
    {
        var result = await EvaluateAsync(expression, options);
        return result is T value ? value : default;
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        _compilationCache.Clear();
        _assemblyCache.Clear();

        lock (_lockObject)
        {
            _statistics.CacheSize = 0;
        }
    }

    /// <summary>
    /// 获取引擎统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public EngineStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            return new EngineStatistics
            {
                TotalExecutions = _statistics.TotalExecutions,
                SuccessfulExecutions = _statistics.SuccessfulExecutions,
                FailedExecutions = _statistics.FailedExecutions,
                CacheHits = _statistics.CacheHits,
                CacheMisses = _statistics.CacheMisses,
                AverageExecutionTimeMs = _statistics.AverageExecutionTimeMs,
                AverageCompilationTimeMs = _statistics.AverageCompilationTimeMs,
                CacheSize = _statistics.CacheSize,
                TotalMemoryUsage = _statistics.TotalMemoryUsage,
                StartTime = _statistics.StartTime
            };
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            ClearCache();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 包装脚本代码
    /// </summary>
    /// <param name="scriptCode">原始脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>包装后的代码</returns>
    private static string WrapScriptCode(string scriptCode, ScriptOptions options)
    {
        var sb = new StringBuilder();

        // 添加using语句
        foreach (var import in options.Imports)
        {
            sb.AppendLine($"using {import};");
        }

        sb.AppendLine();

        return options.ScriptType switch
        {
            ScriptType.Expression => WrapAsExpression(scriptCode, sb),
            ScriptType.Statement => WrapAsStatement(scriptCode, sb),
            ScriptType.Method => WrapAsMethod(scriptCode, sb),
            ScriptType.Class => WrapAsClass(scriptCode, sb),
            ScriptType.Program => scriptCode, // 完整程序不需要包装
            _ => WrapAsStatement(scriptCode, sb)
        };
    }

    /// <summary>
    /// 包装为表达式
    /// </summary>
    private static string WrapAsExpression(string expression, StringBuilder sb)
    {
        sb.AppendLine("public class ScriptClass");
        sb.AppendLine("{");
        sb.AppendLine("    public static object Execute()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return {expression};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 包装为语句
    /// </summary>
    private static string WrapAsStatement(string statements, StringBuilder sb)
    {
        sb.AppendLine("public class ScriptClass");
        sb.AppendLine("{");
        sb.AppendLine("    public static object Execute()");
        sb.AppendLine("    {");
        sb.AppendLine("        object result = null;");
        sb.AppendLine(statements);
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 包装为方法
    /// </summary>
    private static string WrapAsMethod(string method, StringBuilder sb)
    {
        sb.AppendLine("public class ScriptClass");
        sb.AppendLine("{");
        sb.AppendLine(method);
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 包装为类
    /// </summary>
    private static string WrapAsClass(string classCode, StringBuilder sb)
    {
        sb.AppendLine(classCode);
        return sb.ToString();
    }

    /// <summary>
    /// 获取引用程序集
    /// </summary>
    /// <param name="options">脚本选项</param>
    /// <returns>引用程序集</returns>
    private static async Task<IEnumerable<MetadataReference>> GetReferencesAsync(ScriptOptions options)
    {
        var references = new List<MetadataReference>
        {
            // 添加基础引用
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        // 添加.NET运行时引用
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Collections.dll")));

        // 添加用户指定的程序集引用
        foreach (var assembly in options.References)
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        // 添加用户指定的程序集路径引用
        foreach (var path in options.ReferencePaths)
        {
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        // 返回同步结果包装为 Task
        return await Task.FromResult(references);
    }

    /// <summary>
    /// 查找入口点
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>入口点方法</returns>
    private static MethodInfo? FindEntryPoint(Assembly assembly)
    {
        // 查找ScriptClass类的Execute方法
        var type = assembly.GetType("ScriptClass");
        return type?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
    }

    /// <summary>
    /// 调用入口点
    /// </summary>
    /// <param name="entryPoint">入口点方法</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    private static object? InvokeEntryPoint(MethodInfo entryPoint, ScriptOptions options)
    {
        var parameters = entryPoint.GetParameters();
        var args = new object?[parameters.Length];

        // 填充参数(如果有)
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramName = parameters[i].Name;
            if (paramName != null && options.Globals.TryGetValue(paramName, out var value))
            {
                args[i] = value;
            }
        }

        return entryPoint.Invoke(null, args);
    }

    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>缓存键</returns>
    private static string GenerateCacheKey(string scriptCode, ScriptOptions options)
    {
        var hashCode = HashCode.Combine(
            scriptCode,
            options.ScriptType,
            string.Join(",", options.Imports),
            string.Join(",", options.References.Select(r => r.FullName)),
            string.Join(",", options.ReferencePaths));

        return $"script_{hashCode:X}";
    }

    /// <summary>
    /// 更新平均值
    /// </summary>
    /// <param name="currentAverage">当前平均值</param>
    /// <param name="newValue">新值</param>
    /// <param name="count">总数</param>
    /// <returns>新的平均值</returns>
    private static double UpdateAverage(double currentAverage, long newValue, long count)
    {
        return ((currentAverage * (count - 1)) + newValue) / count;
    }

    /// <summary>
    /// 执行已编译的代码
    /// </summary>
    /// <param name="compilationResult">编译结果</param>
    /// <param name="options">脚本选项</param>
    /// <returns>执行结果</returns>
    private static async Task<ScriptResult> ExecuteCompiledAsync(CompilationResult compilationResult, ScriptOptions options)
    {
        if (compilationResult.Assembly == null)
        {
            return ScriptResult.Failure("编译结果无效");
        }

        try
        {
            // 创建取消令牌
            using var cts = new CancellationTokenSource(options.TimeoutMs);

            // 在任务中执行以支持超时
            var task = Task.Run(() =>
            {
                // 加载程序集
                var assembly = Assembly.Load(compilationResult.Assembly);

                // 执行安全检查
                ValidateAssemblySecurity(assembly, options);

                // 查找入口点
                var entryPoint = FindEntryPoint(assembly);
                if (entryPoint == null)
                {
                    return ScriptResult.Failure("未找到有效的入口点");
                }

                // 执行代码
                var result = InvokeEntryPoint(entryPoint, options);
                return ScriptResult.Success(result);
            }, cts.Token);

            return await task;
        }
        catch (OperationCanceledException)
        {
            throw new ScriptTimeoutException(options.TimeoutMs);
        }
        catch (ScriptSecurityException)
        {
            throw; // 重新抛出安全异常
        }
        catch (Exception ex)
        {
            throw new ScriptExecutionException($"脚本执行异常: {ex.Message}", ex, executionTimeMs: options.TimeoutMs);
        }
    }

    /// <summary>
    /// 验证脚本文件安全性
    /// </summary>
    /// <param name="scriptFilePath">脚本文件路径</param>
    /// <param name="securityOptions">安全选项</param>
    /// <exception cref="ScriptSecurityException">安全检查失败时抛出</exception>
    private static void ValidateScriptFileSecurity(string scriptFilePath, SecurityOptions securityOptions)
    {
        if (!securityOptions.EnableSecurityChecks)
        {
            return;
        }

        // 检查文件扩展名
        var extension = Path.GetExtension(scriptFilePath).ToLowerInvariant();

        if (!securityOptions.AllowedFileExtensions.Contains(extension))
        {
            throw new ScriptSecurityException($"不允许的脚本文件扩展名: {extension}", "InvalidFileExtension");
        }

        // 检查文件路径是否包含危险字符
        var dangerousChars = new[] { "..", "~", "$" };
        if (dangerousChars.Any(dc => scriptFilePath.Contains(dc)))
        {
            throw new ScriptSecurityException("脚本文件路径包含危险字符", "DangerousPath");
        }

        // 检查文件大小(防止过大的文件导致内存问题)
        var fileInfo = new FileInfo(scriptFilePath);
        if (fileInfo.Length > securityOptions.MaxFileSize)
        {
            throw new ScriptSecurityException($"脚本文件过大: {fileInfo.Length} 字节，最大允许: {securityOptions.MaxFileSize} 字节", "FileTooLarge");
        }
    }

    /// <summary>
    /// 验证程序集安全性
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="options">脚本选项</param>
    /// <exception cref="ScriptSecurityException">安全检查失败时抛出</exception>
    private static void ValidateAssemblySecurity(Assembly assembly, ScriptOptions options)
    {
        var securityOptions = options.SecurityOptions;

        if (!securityOptions.EnableSecurityChecks)
        {
            return;
        }

        var types = assembly.GetTypes();

        // 如果不允许不安全代码，检查程序集是否包含不安全代码
        if (!options.AllowUnsafe)
        {
            foreach (var type in types)
            {
                // 检查是否使用了 unsafe 关键字或指针类型
                if (type.Name.Contains("Unsafe") || HasUnsafeMembers(type))
                {
                    throw new ScriptSecurityException("脚本包含不安全的代码", "UnsafeCode");
                }
            }
        }

        // 检查是否尝试访问禁止的命名空间
        foreach (var type in types)
        {
            if (securityOptions.ForbiddenNamespaces.Any(ns => type.Namespace?.StartsWith(ns) == true))
            {
                throw new ScriptSecurityException($"脚本尝试访问受限制的命名空间: {type.Namespace}", "RestrictedNamespace");
            }

            // 检查是否使用了禁止的类型
            if (securityOptions.ForbiddenTypes.Any(ft => type.FullName?.Contains(ft) == true))
            {
                throw new ScriptSecurityException($"脚本尝试使用受限制的类型: {type.FullName}", "RestrictedType");
            }
        }

        // 检查方法中的危险调用
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (var method in methods)
            {
                try
                {
                    var methodBody = method.GetMethodBody();
                    if (methodBody != null)
                    {
                        // 这里可以添加更详细的方法体分析
                        // 目前简单检查方法名是否包含危险操作
                        if (securityOptions.DangerousKeywords.Any(keyword => method.Name.Contains(keyword)))
                        {
                            throw new ScriptSecurityException($"脚本包含危险方法调用: {method.Name}", "DangerousMethodCall");
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // 某些方法可能无法获取方法体，忽略这种情况
                }
            }
        }
    }

    /// <summary>
    /// 检查类型是否包含不安全成员
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否包含不安全成员</returns>
    private static bool HasUnsafeMembers(Type type)
    {
        try
        {
            // 检查字段
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (fields.Any(f => f.FieldType.IsPointer))
            {
                return true;
            }

            // 检查方法
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            return methods.Any(m => m.GetParameters().Any(p => p.ParameterType.IsPointer) ||
                                m.ReturnType.IsPointer);
        }
        catch
        {
            // 如果检查过程中发生异常，为了安全起见，假设它包含不安全成员
            return true;
        }
    }

    /// <summary>
    /// 内部编译方法
    /// </summary>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="options">脚本选项</param>
    /// <returns>编译结果</returns>
    private static async Task<CompilationResult> CompileInternalAsync(string scriptCode, ScriptOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 根据脚本类型包装代码
            var wrappedCode = WrapScriptCode(scriptCode, options);

            // 创建语法树
            var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode, new CSharpParseOptions(
                options.CompilerOptions.LanguageVersion));

            // 获取引用
            var references = await GetReferencesAsync(options);

            // 创建编译选项
            var compilationOptions = new CSharpCompilationOptions(
                outputKind: options.OutputKind,
                optimizationLevel: options.OptimizationLevel,
                platform: options.Platform,
                allowUnsafe: options.AllowUnsafe,
                warningLevel: options.CompilerOptions.WarningLevel,
                generalDiagnosticOption: options.CompilerOptions.TreatWarningsAsErrors
                    ? ReportDiagnostic.Error
                    : ReportDiagnostic.Default);

            // 创建编译
            var compilation = CSharpCompilation.Create(
                assemblyName: $"DynamicScript_{Guid.NewGuid():N}",
                syntaxTrees: [syntaxTree],
                references: references,
                options: compilationOptions);

            // 编译到内存
            using var assemblyStream = new MemoryStream();
            using var symbolsStream = options.CompilerOptions.GenerateDebugInfo ? new MemoryStream() : null;

            var emitOptions = new EmitOptions(
                debugInformationFormat: options.CompilerOptions.DebugInformationFormat);

            var emitResult = compilation.Emit(
                peStream: assemblyStream,
                pdbStream: symbolsStream,
                options: emitOptions);

            stopwatch.Stop();

            if (!emitResult.Success)
            {
                return CompilationResult.Failure(emitResult.Diagnostics);
            }

            var result = CompilationResult.Success(
                assembly: assemblyStream.ToArray(),
                symbols: symbolsStream?.ToArray(),
                assemblyName: compilation.AssemblyName);

            result.CompilationTimeMs = stopwatch.ElapsedMilliseconds;
            result.Diagnostics = emitResult.Diagnostics;

            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            return CompilationResult.Failure([]);
        }
    }
}
