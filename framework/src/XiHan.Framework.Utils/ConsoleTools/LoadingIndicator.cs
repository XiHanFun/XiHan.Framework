// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 静态加载指示器工具类
/// </summary>
public static class LoadingIndicator
{
    /// <summary>
    /// 显示加载指示器并执行异步操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="task">要执行的任务</param>
    /// <param name="message">加载消息</param>
    /// <param name="frames">旋转器帧</param>
    /// <param name="delay">帧间隔</param>
    /// <returns>任务结果</returns>
    public static async Task<T> ShowAsync<T>(Task<T> task, string message = "加载中", string[]? frames = null, int delay = 100)
    {
        using var spinner = new ConsoleSpinner(message, frames, delay);
        try
        {
            var result = await task;
            spinner.Stop($"{message} - 完成");
            return result;
        }
        catch (Exception ex)
        {
            spinner.Stop($"{message} - 失败: {ex.Message}", '✗');
            throw;
        }
    }

    /// <summary>
    /// 显示加载指示器并执行异步操作
    /// </summary>
    /// <param name="task">要执行的任务</param>
    /// <param name="message">加载消息</param>
    /// <param name="frames">旋转器帧</param>
    /// <param name="delay">帧间隔</param>
    public static async Task ShowAsync(Task task, string message = "加载中", string[]? frames = null, int delay = 100)
    {
        using var spinner = new ConsoleSpinner(message, frames, delay);
        try
        {
            await task;
            spinner.Stop($"{message} - 完成");
        }
        catch (Exception ex)
        {
            spinner.Stop($"{message} - 失败: {ex.Message}", '✗');
            throw;
        }
    }

    /// <summary>
    /// 显示加载指示器并执行同步操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">要执行的操作</param>
    /// <param name="message">加载消息</param>
    /// <param name="frames">旋转器帧</param>
    /// <param name="delay">帧间隔</param>
    /// <returns>操作结果</returns>
    public static T Show<T>(Func<T> action, string message = "处理中", string[]? frames = null, int delay = 100)
    {
        using var spinner = new ConsoleSpinner(message, frames, delay);
        try
        {
            var result = action();
            spinner.Stop($"{message} - 完成");
            return result;
        }
        catch (Exception ex)
        {
            spinner.Stop($"{message} - 失败: {ex.Message}", '✗');
            throw;
        }
    }

    /// <summary>
    /// 显示加载指示器并执行同步操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="message">加载消息</param>
    /// <param name="frames">旋转器帧</param>
    /// <param name="delay">帧间隔</param>
    public static void Show(Action action, string message = "处理中", string[]? frames = null, int delay = 100)
    {
        using var spinner = new ConsoleSpinner(message, frames, delay);
        try
        {
            action();
            spinner.Stop($"{message} - 完成");
        }
        catch (Exception ex)
        {
            spinner.Stop($"{message} - 失败: {ex.Message}", '✗');
            throw;
        }
    }
}
