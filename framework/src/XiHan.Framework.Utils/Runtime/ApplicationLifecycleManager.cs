#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationLifecycleManager
// Guid:b6c4d3e2-f5g7-8901-bcde-f23456789abc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2024-12-19 上午 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.Utils.Runtime.Enums;
using XiHan.Framework.Utils.Runtime.Events;

namespace XiHan.Framework.Utils.Runtime;

/// <summary>
/// 应用程序生命周期管理器
/// </summary>
public class ApplicationLifecycleManager : IDisposable
{
    private readonly Lock _lockObject = new();
    private readonly List<Func<Task>> _initializeTasks = [];
    private readonly List<Func<Task>> _shutdownTasks = [];
    private readonly List<string> _lifecycleHistory = [];
    private volatile bool _isDisposed;
    private CancellationTokenSource? _shutdownCancellationTokenSource;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ApplicationLifecycleManager()
    {
        // 注册系统事件
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        if (OsPlatformHelper.IsWindows)
        {
            // Windows 特定事件
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        RecordLifecycleEvent("生命周期管理器已创建");
    }

    /// <summary>
    /// 状态改变事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? StateChanged;

    /// <summary>
    /// 应用程序初始化事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? ApplicationInitializing;

    /// <summary>
    /// 应用程序启动事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? ApplicationStarted;

    /// <summary>
    /// 应用程序暂停事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? ApplicationPausing;

    /// <summary>
    /// 应用程序恢复事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? ApplicationResuming;

    /// <summary>
    /// 应用程序关闭事件
    /// </summary>
    public event EventHandler<ApplicationExitEventArgs>? ApplicationExiting;

    /// <summary>
    /// 应用程序已关闭事件
    /// </summary>
    public event EventHandler<ApplicationExitEventArgs>? ApplicationExited;

    /// <summary>
    /// 应用程序出现异常事件
    /// </summary>
    public event EventHandler<LifecycleEventArgs>? ApplicationError;

    /// <summary>
    /// 当前应用程序状态
    /// </summary>
    public ApplicationState CurrentState { get; private set; } = ApplicationState.Uninitialized;

    /// <summary>
    /// 应用程序启动时间
    /// </summary>
    public DateTime? StartTime { get; private set; }

    /// <summary>
    /// 应用程序关闭时间
    /// </summary>
    public DateTime? ShutdownTime { get; private set; }

    /// <summary>
    /// 是否启用自动重启
    /// </summary>
    public bool AutoRestartEnabled { get; set; }

    /// <summary>
    /// 最大重启次数
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>
    /// 重启间隔
    /// </summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 当前重启次数
    /// </summary>
    public int RestartAttempts { get; private set; }

    /// <summary>
    /// 生命周期历史记录
    /// </summary>
    public IReadOnlyList<string> LifecycleHistory => _lifecycleHistory.AsReadOnly();

    /// <summary>
    /// 初始化应用程序
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化任务</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentState != ApplicationState.Uninitialized)
        {
            throw new InvalidOperationException($"应用程序已初始化，当前状态: {CurrentState}");
        }

        await ChangeStateAsync(ApplicationState.Initializing, "开始初始化应用程序");

        try
        {
            var eventArgs = new LifecycleEventArgs
            {
                State = ApplicationState.Initializing,
                PreviousState = ApplicationState.Uninitialized,
                Message = "应用程序正在初始化"
            };

            ApplicationInitializing?.Invoke(this, eventArgs);

            if (eventArgs.Cancel)
            {
                await ChangeStateAsync(ApplicationState.Uninitialized, "初始化被取消");
                return;
            }

            // 执行初始化任务
            foreach (var task in _initializeTasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await task();
            }

            StartTime = DateTime.Now;
            await ChangeStateAsync(ApplicationState.Running, "应用程序初始化完成");

            ApplicationStarted?.Invoke(this, new LifecycleEventArgs
            {
                State = ApplicationState.Running,
                PreviousState = ApplicationState.Initializing,
                Message = "应用程序已启动"
            });
        }
        catch (OperationCanceledException)
        {
            await ChangeStateAsync(ApplicationState.Uninitialized, "初始化被取消");
            throw;
        }
        catch (Exception ex)
        {
            await ChangeStateAsync(ApplicationState.Error, $"初始化失败: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 暂停应用程序
    /// </summary>
    /// <param name="reason">暂停原因</param>
    public async Task PauseAsync(string? reason = null)
    {
        if (CurrentState != ApplicationState.Running)
        {
            throw new InvalidOperationException($"只有运行中的应用程序才能暂停，当前状态: {CurrentState}");
        }

        await ChangeStateAsync(ApplicationState.Pausing, reason ?? "暂停应用程序");

        var eventArgs = new LifecycleEventArgs
        {
            State = ApplicationState.Pausing,
            PreviousState = ApplicationState.Running,
            Message = reason ?? "应用程序正在暂停",
            CanCancel = true
        };

        ApplicationPausing?.Invoke(this, eventArgs);

        if (eventArgs.Cancel)
        {
            await ChangeStateAsync(ApplicationState.Running, "暂停被取消");
            return;
        }

        await ChangeStateAsync(ApplicationState.Paused, "应用程序已暂停");
    }

    /// <summary>
    /// 恢复应用程序
    /// </summary>
    /// <param name="reason">恢复原因</param>
    public async Task ResumeAsync(string? reason = null)
    {
        if (CurrentState != ApplicationState.Paused)
        {
            throw new InvalidOperationException($"只有暂停中的应用程序才能恢复，当前状态: {CurrentState}");
        }

        await ChangeStateAsync(ApplicationState.Resuming, reason ?? "恢复应用程序");

        var eventArgs = new LifecycleEventArgs
        {
            State = ApplicationState.Resuming,
            PreviousState = ApplicationState.Paused,
            Message = reason ?? "应用程序正在恢复"
        };

        ApplicationResuming?.Invoke(this, eventArgs);

        await ChangeStateAsync(ApplicationState.Running, "应用程序已恢复");
    }

    /// <summary>
    /// 关闭应用程序
    /// </summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="exitCode">退出代码</param>
    /// <param name="timeout">关闭超时时间</param>
    public async Task ShutdownAsync(ApplicationExitReason reason = ApplicationExitReason.Normal,
        int exitCode = 0, TimeSpan? timeout = null)
    {
        if (CurrentState is ApplicationState.Shutdown or ApplicationState.Shutting)
        {
            return;
        }

        _shutdownCancellationTokenSource = new CancellationTokenSource();
        if (timeout.HasValue)
        {
            _shutdownCancellationTokenSource.CancelAfter(timeout.Value);
        }

        await ChangeStateAsync(ApplicationState.Shutting, $"开始关闭应用程序，原因: {reason}");

        try
        {
            var exitEventArgs = new ApplicationExitEventArgs
            {
                Reason = reason,
                ExitCode = exitCode,
                Message = $"应用程序正在关闭，原因: {reason}",
                CanCancel = reason != ApplicationExitReason.Forced
            };

            ApplicationExiting?.Invoke(this, exitEventArgs);

            if (exitEventArgs.Cancel && reason != ApplicationExitReason.Forced)
            {
                await ChangeStateAsync(ApplicationState.Running, "关闭被取消");
                return;
            }

            // 执行关闭任务
            foreach (var task in _shutdownTasks)
            {
                try
                {
                    _shutdownCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await task();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RecordLifecycleEvent($"关闭任务执行异常: {ex.Message}");
                }
            }

            ShutdownTime = DateTime.Now;
            await ChangeStateAsync(ApplicationState.Shutdown, "应用程序已关闭");

            ApplicationExited?.Invoke(this, new ApplicationExitEventArgs
            {
                Reason = reason,
                ExitCode = exitCode,
                Message = "应用程序已关闭"
            });

            // 如果不是重启，则退出进程
            if (reason != ApplicationExitReason.Restart)
            {
                Environment.Exit(exitCode);
            }
        }
        catch (OperationCanceledException)
        {
            await ChangeStateAsync(ApplicationState.Error, "关闭操作超时");
            Environment.Exit(-1);
        }
        catch (Exception ex)
        {
            await ChangeStateAsync(ApplicationState.Error, $"关闭过程中发生异常: {ex.Message}", ex);
            Environment.Exit(-1);
        }
    }

    /// <summary>
    /// 重启应用程序
    /// </summary>
    /// <param name="reason">重启原因</param>
    public async Task RestartAsync(string? reason = null)
    {
        RecordLifecycleEvent($"准备重启应用程序: {reason}");

        if (RestartAttempts >= MaxRestartAttempts)
        {
            throw new InvalidOperationException($"超过最大重启次数 {MaxRestartAttempts}");
        }

        RestartAttempts++;

        // 先关闭应用程序
        await ShutdownAsync(ApplicationExitReason.Restart);

        // 等待重启延迟
        if (RestartDelay > TimeSpan.Zero)
        {
            await Task.Delay(RestartDelay);
        }

        // 启动新进程
        var currentProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = currentProcess.MainModule?.FileName ?? Environment.ProcessPath!,
            Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
            UseShellExecute = true
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }

    /// <summary>
    /// 强制退出应用程序
    /// </summary>
    /// <param name="exitCode">退出代码</param>
    public void ForceExit(int exitCode = -1)
    {
        RecordLifecycleEvent($"强制退出应用程序，退出代码: {exitCode}");
        Environment.Exit(exitCode);
    }

    /// <summary>
    /// 添加初始化任务
    /// </summary>
    /// <param name="task">初始化任务</param>
    public void AddInitializeTask(Func<Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (_lockObject)
        {
            _initializeTasks.Add(task);
        }
    }

    /// <summary>
    /// 添加关闭任务
    /// </summary>
    /// <param name="task">关闭任务</param>
    public void AddShutdownTask(Func<Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (_lockObject)
        {
            _shutdownTasks.Add(task);
        }
    }

    /// <summary>
    /// 获取应用程序运行时间
    /// </summary>
    /// <returns>运行时间</returns>
    public TimeSpan GetUptime()
    {
        return StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;
    }

    /// <summary>
    /// 获取生命周期摘要
    /// </summary>
    /// <returns>生命周期摘要字符串</returns>
    public string GetLifecycleSummary()
    {
        var uptime = GetUptime();
        return $"应用程序生命周期摘要:\n" +
               $"  当前状态: {CurrentState}\n" +
               $"  启动时间: {StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未启动"}\n" +
               $"  运行时间: {uptime:dd\\.hh\\:mm\\:ss}\n" +
               $"  重启次数: {RestartAttempts}/{MaxRestartAttempts}\n" +
               $"  自动重启: {(AutoRestartEnabled ? "启用" : "禁用")}\n" +
               $"  生命周期事件数: {_lifecycleHistory.Count}";
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            // 如果应用程序还在运行，尝试正常关闭
            if (CurrentState is ApplicationState.Running or ApplicationState.Paused)
            {
                ShutdownAsync().Wait(TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // 忽略关闭异常
        }

        _shutdownCancellationTokenSource?.Dispose();

        // 注销事件
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

        if (OsPlatformHelper.IsWindows)
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        RecordLifecycleEvent("生命周期管理器已释放");

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 改变应用程序状态
    /// </summary>
    /// <param name="newState">新状态</param>
    /// <param name="message">状态消息</param>
    /// <param name="exception">相关异常</param>
    private async Task ChangeStateAsync(ApplicationState newState, string message, Exception? exception = null)
    {
        var previousState = CurrentState;
        CurrentState = newState;

        RecordLifecycleEvent($"状态变更: {previousState} -> {newState}, {message}");

        var eventArgs = new LifecycleEventArgs
        {
            State = newState,
            PreviousState = previousState,
            Message = message,
            Exception = exception
        };

        StateChanged?.Invoke(this, eventArgs);

        if (exception != null)
        {
            ApplicationError?.Invoke(this, eventArgs);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 记录生命周期事件
    /// </summary>
    /// <param name="message">事件消息</param>
    private void RecordLifecycleEvent(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {message}";

        lock (_lockObject)
        {
            _lifecycleHistory.Add(logEntry);

            // 限制历史记录数量
            if (_lifecycleHistory.Count > 1000)
            {
                _lifecycleHistory.RemoveAt(0);
            }
        }

        Debug.WriteLine(logEntry);
    }

    /// <summary>
    /// 处理进程退出事件
    /// </summary>
    private void OnProcessExit(object? sender, EventArgs e)
    {
        RecordLifecycleEvent("接收到进程退出信号");

        if (CurrentState is not ApplicationState.Shutdown and not ApplicationState.Shutting)
        {
            // 异步关闭可能无法完成，使用同步方式
            Task.Run(async () => await ShutdownAsync(ApplicationExitReason.SystemShutdown)).Wait(5000);
        }
    }

    /// <summary>
    /// 处理未处理异常事件
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        RecordLifecycleEvent($"发生未处理异常: {exception?.Message}");

        ChangeStateAsync(ApplicationState.Error, "发生未处理异常", exception).Wait();

        if (AutoRestartEnabled && RestartAttempts < MaxRestartAttempts)
        {
            Task.Run(async () => await RestartAsync("因未处理异常自动重启"));
        }
        else
        {
            Task.Run(async () => await ShutdownAsync(ApplicationExitReason.Exception, -1)).Wait(5000);
        }
    }

    /// <summary>
    /// 处理 Ctrl+C 事件 (Windows)
    /// </summary>
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        RecordLifecycleEvent("接收到取消键按下信号 (Ctrl+C)");

        e.Cancel = true; // 阻止立即退出
        Task.Run(async () => await ShutdownAsync(ApplicationExitReason.UserRequested));
    }
}
