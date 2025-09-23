#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleProgressBar
// Guid:95f7e832-a4b1-4c29-9a8b-76fd84c18133
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台进度条，支持百分比、时间估计、速率显示
/// </summary>
public class ConsoleProgressBar : IDisposable
{
    private readonly int _width;
    private readonly string _progressChar;
    private readonly string _emptyChar;
    private readonly DateTime _startTime;
    private readonly int _currentRow;
    private long _total;
    private long _current;
    private bool _disposed;

    /// <summary>
    /// 创建一个新的控制台进度条
    /// </summary>
    /// <param name="total">总数量</param>
    /// <param name="width">进度条宽度</param>
    /// <param name="progressChar">进度字符</param>
    /// <param name="emptyChar">空白字符</param>
    public ConsoleProgressBar(long total, int width = 50, string progressChar = "█", string emptyChar = "░")
    {
        _total = total;
        _width = width;
        _progressChar = progressChar;
        _emptyChar = emptyChar;
        _startTime = DateTime.Now;
        _currentRow = Console.CursorTop;

        // 预留空行
        Console.WriteLine();
        Console.SetCursorPosition(0, _currentRow);
    }

    /// <summary>
    /// 更新进度
    /// </summary>
    /// <param name="current">当前进度</param>
    /// <param name="message">附加消息</param>
    public void Update(long current, string? message = null)
    {
        if (_disposed)
        {
            return;
        }

        _current = Math.Min(current, _total);
        DrawProgress(message);
    }

    /// <summary>
    /// 增加进度
    /// </summary>
    /// <param name="increment">增量</param>
    /// <param name="message">附加消息</param>
    public void Increment(long increment = 1, string? message = null)
    {
        Update(_current + increment, message);
    }

    /// <summary>
    /// 设置总数量
    /// </summary>
    /// <param name="total">总数量</param>
    public void SetTotal(long total)
    {
        _total = total;
        DrawProgress();
    }

    /// <summary>
    /// 完成进度条
    /// </summary>
    public void Complete(string? message = null)
    {
        Update(_total, message ?? "完成");
        Console.WriteLine();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_current < _total)
            {
                Complete();
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 格式化速率显示
    /// </summary>
    /// <param name="rate">速率</param>
    /// <returns>格式化的速率字符串</returns>
    private static string FormatRate(double rate)
    {
        return rate switch
        {
            >= 1000000 => $"{rate / 1000000:F1}M",
            >= 1000 => $"{rate / 1000:F1}K",
            _ => $"{rate:F1}"
        };
    }

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>格式化的时间字符串</returns>
    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    /// <summary>
    /// 绘制进度条
    /// </summary>
    /// <param name="message">附加消息</param>
    private void DrawProgress(string? message = null)
    {
        var originalTop = Console.CursorTop;
        var originalLeft = Console.CursorLeft;

        try
        {
            Console.SetCursorPosition(0, _currentRow);

            // 计算进度百分比
            var percentage = _total > 0 ? (double)_current / _total : 0;
            var progressWidth = (int)(percentage * _width);

            // 构建进度条
            var progressBar = new StringBuilder();
            progressBar.Append('[');
            progressBar.Append(new string(_progressChar[0], progressWidth));
            progressBar.Append(new string(_emptyChar[0], _width - progressWidth));
            progressBar.Append(']');

            // 计算百分比、速率和预估时间
            var percent = percentage * 100;
            var elapsed = DateTime.Now - _startTime;
            var rate = elapsed.TotalSeconds > 0 ? _current / elapsed.TotalSeconds : 0;
            var remaining = rate > 0 && _current < _total ? TimeSpan.FromSeconds((_total - _current) / rate) : TimeSpan.Zero;

            // 输出进度信息
            var info = $" {percent:F1}% ({_current}/{_total}) ";
            if (rate > 0)
            {
                info += $"[{FormatRate(rate)}/s] ";
            }
            if (remaining > TimeSpan.Zero)
            {
                info += $"ETA: {FormatTime(remaining)} ";
            }
            if (!string.IsNullOrEmpty(message))
            {
                info += $"- {message}";
            }

            var output = progressBar + info;

            // 清除当前行并输出新内容
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1));
            Console.Write("\r" + output);
        }
        catch
        {
            // 忽略控制台操作异常
        }
        finally
        {
            try
            {
                if (originalTop != _currentRow)
                {
                    Console.SetCursorPosition(originalLeft, originalTop);
                }
            }
            catch
            {
                // 忽略光标定位异常
            }
        }
    }
}

/// <summary>
/// 多任务进度条管理器
/// </summary>
public class ConsoleMultiProgressBar : IDisposable
{
    private readonly Dictionary<string, ProgressTask> _tasks = [];
    private readonly int _startRow;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// 创建多任务进度条管理器
    /// </summary>
    public ConsoleMultiProgressBar()
    {
        _startRow = Console.CursorTop;
    }

    /// <summary>
    /// 添加任务
    /// </summary>
    /// <param name="taskId">任务Id</param>
    /// <param name="total">总数量</param>
    /// <param name="description">任务描述</param>
    /// <param name="width">进度条宽度</param>
    public void AddTask(string taskId, long total, string description = "", int width = 30)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (!_tasks.ContainsKey(taskId))
            {
                var row = _startRow + _tasks.Count;
                _tasks[taskId] = new ProgressTask(taskId, total, description, row, width);

                // 确保有足够的行
                var currentRow = Console.CursorTop;
                if (currentRow <= row)
                {
                    for (var i = currentRow; i <= row; i++)
                    {
                        Console.WriteLine();
                    }
                }

                UpdateTask(taskId, 0);
            }
        }
    }

    /// <summary>
    /// 更新任务进度
    /// </summary>
    /// <param name="taskId">任务Id</param>
    /// <param name="current">当前进度</param>
    /// <param name="message">附加消息</param>
    public void UpdateTask(string taskId, long current, string? message = null)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Update(current, message);
                DrawTask(task);
            }
        }
    }

    /// <summary>
    /// 增加任务进度
    /// </summary>
    /// <param name="taskId">任务Id</param>
    /// <param name="increment">增量</param>
    /// <param name="message">附加消息</param>
    public void IncrementTask(string taskId, long increment = 1, string? message = null)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Current = Math.Min(task.Current + increment, task.Total);
                UpdateTask(taskId, task.Current, message);
            }
        }
    }

    /// <summary>
    /// 完成任务
    /// </summary>
    /// <param name="taskId">任务Id</param>
    /// <param name="message">完成消息</param>
    public void CompleteTask(string taskId, string? message = null)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.IsCompleted = true;
                UpdateTask(taskId, task.Total, message ?? "完成");
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
            lock (_lock)
            {
                // 移动光标到所有进度条下方
                var maxRow = _tasks.Values.Count != 0 ? _tasks.Values.Max(t => t.Row) + 1 : Console.CursorTop;
                Console.SetCursorPosition(0, maxRow);
                Console.WriteLine();
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 绘制任务进度
    /// </summary>
    /// <param name="task">任务</param>
    private static void DrawTask(ProgressTask task)
    {
        var originalTop = Console.CursorTop;
        var originalLeft = Console.CursorLeft;

        try
        {
            Console.SetCursorPosition(0, task.Row);

            var percentage = task.Total > 0 ? (double)task.Current / task.Total : 0;
            var progressWidth = (int)(percentage * task.Width);

            var progressBar = new StringBuilder();
            progressBar.Append('[');
            progressBar.Append(new string('█', progressWidth));
            progressBar.Append(new string('░', task.Width - progressWidth));
            progressBar.Append(']');

            var info = $" {percentage * 100:F1}% ({task.Current}/{task.Total})";
            if (!string.IsNullOrEmpty(task.Description))
            {
                info = $" {task.Description} -" + info;
            }
            if (!string.IsNullOrEmpty(task.Message))
            {
                info += $" - {task.Message}";
            }
            if (task.IsCompleted)
            {
                info += " ✓";
            }

            var output = progressBar + info;

            // 清除当前行并输出新内容
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1));
            Console.Write("\r" + output);
        }
        catch
        {
            // 忽略控制台操作异常
        }
        finally
        {
            try
            {
                Console.SetCursorPosition(originalLeft, originalTop);
            }
            catch
            {
                // 忽略光标定位异常
            }
        }
    }

    /// <summary>
    /// 进度任务
    /// </summary>
    private class ProgressTask
    {
        public ProgressTask(string id, long total, string description, int row, int width)
        {
            Id = id;
            Total = total;
            Description = description;
            Row = row;
            Width = width;
        }

        public string Id { get; }
        public long Total { get; }
        public long Current { get; set; }
        public string Description { get; }
        public int Row { get; }
        public int Width { get; }
        public string? Message { get; set; }
        public bool IsCompleted { get; set; }

        public void Update(long current, string? message)
        {
            Current = Math.Min(current, Total);
            Message = message;
        }
    }
}
