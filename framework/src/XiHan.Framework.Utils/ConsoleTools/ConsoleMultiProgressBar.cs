#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleMultiProgressBar
// Guid:70ae1e3a-7d64-4324-b4dc-7ea7c203f2c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:19:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.ConsoleTools;

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
