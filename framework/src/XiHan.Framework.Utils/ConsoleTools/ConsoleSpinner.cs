#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleSpinner
// Guid:c8f9e123-b5d2-4e39-8c7b-99fd84c18134
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台旋转器/加载指示器
/// </summary>
public class ConsoleSpinner : IDisposable
{
    private readonly string[] _frames;
    private readonly int _delay;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _spinTask;
    private readonly string _message;
    private readonly int _startRow;
    private readonly int _startColumn;
    private int _currentFrame;
    private bool _disposed;

    /// <summary>
    /// 创建一个新的控制台旋转器
    /// </summary>
    /// <param name="message">显示消息</param>
    /// <param name="frames">旋转器帧</param>
    /// <param name="delay">帧间隔（毫秒）</param>
    public ConsoleSpinner(string message = "加载中", string[]? frames = null, int delay = 100)
    {
        _message = message;
        _frames = frames ?? ConsoleSpinnerStyles.Classic;
        _delay = delay;
        _startRow = Console.CursorTop;
        _startColumn = Console.CursorLeft;
        _cancellationTokenSource = new CancellationTokenSource();

        // 隐藏光标
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // 某些环境下可能不支持隐藏光标
        }

        // 启动旋转任务
        _spinTask = Task.Run(SpinAsync, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// 更新消息
    /// </summary>
    /// <param name="newMessage">新消息</param>
    public void UpdateMessage(string newMessage)
    {
        if (_disposed)
        {
            return;
        }

        var field = typeof(ConsoleSpinner).GetField("_message",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, newMessage);
    }

    /// <summary>
    /// 停止旋转器并显示完成消息
    /// </summary>
    /// <param name="finalMessage">最终消息</param>
    /// <param name="successChar">成功字符</param>
    public void Stop(string? finalMessage = null, char successChar = '✓')
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _cancellationTokenSource.Cancel();
            _spinTask.Wait(TimeSpan.FromMilliseconds(500)); // 等待最多500ms

            // 清除旋转器并显示最终消息
            Console.SetCursorPosition(_startColumn, _startRow);
            var clearLength = Math.Max($"{successChar} {finalMessage ?? _message}".Length, 50);
            Console.Write(new string(' ', clearLength));

            Console.SetCursorPosition(_startColumn, _startRow);
            Console.Write($"{successChar} {finalMessage ?? _message}");
            Console.WriteLine();
        }
        catch
        {
            // 忽略异常
        }
        finally
        {
            try
            {
                Console.CursorVisible = true;
            }
            catch
            {
                // 某些环境下可能不支持显示光标
            }
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步旋转任务
    /// </summary>
    private async Task SpinAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                DrawFrame();
                _currentFrame = (_currentFrame + 1) % _frames.Length;

                await Task.Delay(_delay, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消操作
        }
        catch
        {
            // 忽略其他异常
        }
    }

    /// <summary>
    /// 绘制当前帧
    /// </summary>
    private void DrawFrame()
    {
        if (_disposed)
        {
            return;
        }

        var originalTop = Console.CursorTop;
        var originalLeft = Console.CursorLeft;

        try
        {
            Console.SetCursorPosition(_startColumn, _startRow);
            var output = $"{_frames[_currentFrame]} {_message}";

            // 清除当前行内容
            var clearLength = Math.Max(output.Length, 50); // 确保清除足够长度
            Console.Write(new string(' ', clearLength));

            // 重新定位并输出新内容
            Console.SetCursorPosition(_startColumn, _startRow);
            Console.Write(output);
        }
        catch
        {
            // 忽略控制台操作异常
        }
        finally
        {
            try
            {
                // 恢复原始光标位置（如果不是在同一行）
                if (originalTop != _startRow || originalLeft < _startColumn + $"{_frames[_currentFrame]} {_message}".Length)
                {
                    Console.SetCursorPosition(_startColumn + $"{_frames[_currentFrame]} {_message}".Length, _startRow);
                }
            }
            catch
            {
                // 忽略光标定位异常
            }
        }
    }
}
