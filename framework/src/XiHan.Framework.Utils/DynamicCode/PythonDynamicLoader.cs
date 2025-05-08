#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PythonDynamicLoader
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2024-07-14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace XiHan.Framework.Utils.DynamicCode;

/// <summary>
/// Python 动态代码加载和执行工具类
/// </summary>
public static class PythonDynamicLoader
{
    /// <summary>
    /// Python 路径
    /// </summary>
    private static string _pythonPath = "python";

    /// <summary>
    /// 默认的执行模式
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>
        /// 直接执行（-c 参数）
        /// </summary>
        Direct,

        /// <summary>
        /// 通过标准输入执行
        /// </summary>
        StdIn,

        /// <summary>
        /// 通过临时文件执行
        /// </summary>
        TempFile
    }

    /// <summary>
    /// 默认执行模式
    /// </summary>
    public static ExecutionMode DefaultMode { get; set; } = ExecutionMode.Direct;

    /// <summary>
    /// 设置 Python 解释器的路径
    /// </summary>
    /// <param name="pythonPath">Python 解释器路径</param>
    public static void SetPythonPath(string pythonPath)
    {
        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            throw new ArgumentException("Python 解释器路径不能为空", nameof(pythonPath));
        }

        _pythonPath = pythonPath;
    }

    /// <summary>
    /// 执行 Python 代码
    /// </summary>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    public static string ExecuteCode(string pythonCode, params string[] args)
    {
        return ExecuteCode(pythonCode, DefaultMode, args);
    }

    /// <summary>
    /// 执行 Python 代码，指定执行模式
    /// </summary>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="mode">执行模式</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    public static string ExecuteCode(string pythonCode, ExecutionMode mode, params string[] args)
    {
        return mode switch
        {
            ExecutionMode.Direct => ExecuteCodeDirect(pythonCode, args),
            ExecutionMode.StdIn => ExecuteCodeViaStdIn(pythonCode, args),
            ExecutionMode.TempFile => ExecuteCodeViaTempFile(pythonCode, args),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的执行模式"),
        };
    }

    /// <summary>
    /// 执行 Python 文件
    /// </summary>
    /// <param name="filePath">Python 文件路径</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    public static string ExecuteFile(string filePath, params string[] args)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到 Python 文件", filePath);
        }

        // 构建参数字符串
        var arguments = $"\"{filePath}\"";
        if (args?.Length > 0)
        {
            arguments += " " + string.Join(" ", args.Select(arg => $"\"{arg}\""));
        }

        // 执行 Python 解释器
        return ExecutePythonProcess(_pythonPath, arguments);
    }

    /// <summary>
    /// 执行 Python 代码并获取结果对象
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="args">命令行参数</param>
    /// <returns>反序列化的结果对象</returns>
    public static T? ExecuteCodeAndGetResult<T>(string pythonCode, params string[] args)
    {
        // 在 Python 代码末尾添加 JSON 序列化
        var jsonCode = pythonCode + @"
import json
import sys

# 确保最后一行是函数调用或返回值
# 将结果转换为 JSON
result = locals().get('result', None)
json.dump(result, sys.stdout)
";
        var jsonResult = ExecuteCode(jsonCode, args);

        if (string.IsNullOrWhiteSpace(jsonResult))
        {
            return default;
        }

        // 反序列化 JSON 结果
        return JsonSerializer.Deserialize<T>(jsonResult);
    }

    /// <summary>
    /// 检查 Python 是否已安装
    /// </summary>
    /// <returns>是否已安装</returns>
    public static bool IsPythonInstalled()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取安装的 Python 版本
    /// </summary>
    /// <returns>Python 版本</returns>
    public static string GetPythonVersion()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Trim();
        }
        catch (Exception ex)
        {
            return $"获取 Python 版本时出错: {ex.Message}";
        }
    }

    /// <summary>
    /// 通过 -c 参数直接执行 Python 代码
    /// </summary>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    private static string ExecuteCodeDirect(string pythonCode, params string[] args)
    {
        // 准备 Python 代码（替换引号并处理多行）
        var escapedCode = pythonCode
            .Replace("\"", "\\\"")
            .Replace(Environment.NewLine, "; ");

        // 构建参数字符串：-c "代码" arg1 arg2 ...
        var arguments = $"-c \"{escapedCode}\"";
        if (args?.Length > 0)
        {
            arguments += " " + string.Join(" ", args.Select(arg => $"\"{arg}\""));
        }

        // 执行 Python 解释器
        return ExecutePythonProcess(_pythonPath, arguments);
    }

    /// <summary>
    /// 通过标准输入执行 Python 代码
    /// </summary>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    private static string ExecuteCodeViaStdIn(string pythonCode, params string[] args)
    {
        // 构建参数字符串：-u（无缓冲输出）arg1 arg2 ...
        var arguments = "-u";
        if (args?.Length > 0)
        {
            arguments += " " + string.Join(" ", args.Select(arg => $"\"{arg}\""));
        }

        // 启动 Python 进程
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        // 输出缓冲区
        var output = new StringBuilder();
        var error = new StringBuilder();

        // 异步读取输出和错误流
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 将代码写入标准输入
            process.StandardInput.Write(pythonCode);
            process.StandardInput.Close(); // 关闭标准输入，告诉 Python 没有更多输入了

            process.WaitForExit();

            // 如果进程返回非零值，表示有错误
            return process.ExitCode != 0
                ? throw new InvalidOperationException(
                    $"Python 执行错误 (ExitCode={process.ExitCode}):\n{error}")
                : output.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"执行 Python 脚本时出错: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 通过临时文件执行 Python 代码
    /// </summary>
    /// <param name="pythonCode">Python 代码</param>
    /// <param name="args">命令行参数</param>
    /// <returns>标准输出</returns>
    private static string ExecuteCodeViaTempFile(string pythonCode, params string[] args)
    {
        // 创建临时文件保存 Python 代码
        var tempFilePath = Path.GetTempFileName();
        try
        {
            // 写入 Python 代码到临时文件
            File.WriteAllText(tempFilePath, pythonCode, Encoding.UTF8);

            // 执行临时文件
            return ExecuteFile(tempFilePath, args);
        }
        finally
        {
            // 删除临时文件
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // 忽略删除失败的异常
                }
            }
        }
    }

    /// <summary>
    /// 执行 Python 进程并获取输出
    /// </summary>
    /// <param name="fileName">文件名（Python 解释器路径）</param>
    /// <param name="arguments">参数</param>
    /// <returns>标准输出</returns>
    private static string ExecutePythonProcess(string fileName, string arguments)
    {
        // 启动 Python 进程
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        // 输出缓冲区
        var output = new StringBuilder();
        var error = new StringBuilder();

        // 异步读取输出和错误流
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            // 如果进程返回非零值，表示有错误
            return process.ExitCode != 0
                ? throw new InvalidOperationException(
                    $"Python 执行错误 (ExitCode={process.ExitCode}):\n{error}")
                : output.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"执行 Python 脚本时出错: {ex.Message}", ex);
        }
    }
}
