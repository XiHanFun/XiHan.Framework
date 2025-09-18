#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleMenu
// Guid:f1g2h456-e8g5-7162-bf0e-c2fd84c18137
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台交互式菜单
/// </summary>
public class ConsoleMenu
{
    private readonly List<MenuItem> _menuItems = [];
    private int _selectedIndex = 0;
    private bool _exitRequested = false;

    /// <summary>
    /// 菜单标题
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// 是否显示退出选项
    /// </summary>
    public bool ShowExitOption { get; set; } = true;

    /// <summary>
    /// 退出选项文本
    /// </summary>
    public string ExitOptionText { get; set; } = "退出";

    /// <summary>
    /// 选择前缀
    /// </summary>
    public string SelectionPrefix { get; set; } = "► ";

    /// <summary>
    /// 未选择前缀
    /// </summary>
    public string NonSelectionPrefix { get; set; } = "  ";

    /// <summary>
    /// 选择颜色
    /// </summary>
    public ConsoleColor SelectionColor { get; set; } = ConsoleColor.Yellow;

    /// <summary>
    /// 普通颜色
    /// </summary>
    public ConsoleColor NormalColor { get; set; } = ConsoleColor.White;

    /// <summary>
    /// 标题颜色
    /// </summary>
    public ConsoleColor TitleColor { get; set; } = ConsoleColor.Cyan;

    /// <summary>
    /// 添加菜单项
    /// </summary>
    /// <param name="text">显示文本</param>
    /// <param name="action">执行动作</param>
    /// <param name="isEnabled">是否启用</param>
    /// <returns>菜单实例</returns>
    public ConsoleMenu AddItem(string text, Action? action = null, bool isEnabled = true)
    {
        _menuItems.Add(new MenuItem(text, action, isEnabled));
        return this;
    }

    /// <summary>
    /// 添加菜单项（带返回值）
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="text">显示文本</param>
    /// <param name="func">执行函数</param>
    /// <param name="isEnabled">是否启用</param>
    /// <returns>菜单实例</returns>
    public ConsoleMenu AddItem<T>(string text, Func<T>? func = null, bool isEnabled = true)
    {
        _menuItems.Add(new MenuItem(text, func == null ? null : () => func(), isEnabled));
        return this;
    }

    /// <summary>
    /// 添加分隔符
    /// </summary>
    /// <param name="text">分隔符文本</param>
    /// <returns>菜单实例</returns>
    public ConsoleMenu AddSeparator(string text = "─────────")
    {
        _menuItems.Add(new MenuItem(text, null, false) { IsSeparator = true });
        return this;
    }

    /// <summary>
    /// 添加子菜单
    /// </summary>
    /// <param name="text">显示文本</param>
    /// <param name="subMenu">子菜单</param>
    /// <returns>菜单实例</returns>
    public ConsoleMenu AddSubMenu(string text, ConsoleMenu subMenu)
    {
        _menuItems.Add(new MenuItem(text, () => subMenu.Show(), true) { IsSubMenu = true });
        return this;
    }

    /// <summary>
    /// 显示菜单并等待用户选择
    /// </summary>
    /// <returns>选择的菜单项索引，-1表示退出</returns>
    public int Show()
    {
        if (_menuItems.Count == 0)
        {
            ConsoleColorWriter.WriteError("菜单没有任何选项");
            return -1;
        }

        _selectedIndex = GetFirstEnabledIndex();
        _exitRequested = false;

        try
        {
            // 隐藏光标
            Console.CursorVisible = false;

            while (!_exitRequested)
            {
                DrawMenu();
                HandleKeyPress();
            }

            return _selectedIndex;
        }
        finally
        {
            // 恢复光标
            try
            {
                Console.CursorVisible = true;
            }
            catch
            {
                // 忽略异常
            }
        }
    }

    /// <summary>
    /// 显示菜单并执行选择的动作
    /// </summary>
    public void ShowAndExecute()
    {
        while (true)
        {
            var selectedIndex = Show();

            if (selectedIndex == -1) // 退出
            {
                break;
            }

            if (selectedIndex < _menuItems.Count)
            {
                var item = _menuItems[selectedIndex];
                if (item.IsEnabled && !item.IsSeparator)
                {
                    try
                    {
                        item.Action?.Invoke();

                        if (!item.IsSubMenu)
                        {
                            Console.WriteLine();
                            ConsoleColorWriter.WriteInfo("按任意键返回菜单...");
                            Console.ReadKey();
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleColorWriter.WriteError($"执行菜单项时发生错误: {ex.Message}");
                        Console.WriteLine("按任意键继续...");
                        Console.ReadKey();
                    }
                }
            }

            // 重置选择状态
            _exitRequested = false;
        }
    }

    /// <summary>
    /// 绘制菜单
    /// </summary>
    private void DrawMenu()
    {
        Console.Clear();

        // 显示标题
        if (!string.IsNullOrEmpty(Title))
        {
            ConsoleColorWriter.WriteColoredMessage(Title, TitleColor);
            Console.WriteLine(new string('═', Math.Min(Title.Length, Console.WindowWidth - 1)));
            Console.WriteLine();
        }

        // 显示菜单项
        for (var i = 0; i < _menuItems.Count; i++)
        {
            var item = _menuItems[i];
            var isSelected = i == _selectedIndex && !item.IsSeparator;

            if (item.IsSeparator)
            {
                ConsoleColorWriter.WriteColoredMessage($"  {item.Text}", ConsoleColor.DarkGray);
                continue;
            }

            var prefix = isSelected ? SelectionPrefix : NonSelectionPrefix;
            var color = isSelected ? SelectionColor : (item.IsEnabled ? NormalColor : ConsoleColor.DarkGray);

            var displayText = $"{prefix}{i + 1}. {item.Text}";
            if (item.IsSubMenu)
            {
                displayText += " ►";
            }

            ConsoleColorWriter.WriteColoredMessage(displayText, color);
        }

        // 显示退出选项
        if (ShowExitOption)
        {
            Console.WriteLine();
            var exitIndex = _menuItems.Count;
            var isExitSelected = _selectedIndex == exitIndex;
            var exitPrefix = isExitSelected ? SelectionPrefix : NonSelectionPrefix;
            var exitColor = isExitSelected ? SelectionColor : NormalColor;

            ConsoleColorWriter.WriteColoredMessage($"{exitPrefix}0. {ExitOptionText}", exitColor);
        }

        // 显示帮助信息
        Console.WriteLine();
        ConsoleColorWriter.WriteColoredMessage("使用 ↑↓ 或 数字键选择，回车确认，ESC 退出", ConsoleColor.Gray);
    }

    /// <summary>
    /// 处理按键
    /// </summary>
    private void HandleKeyPress()
    {
        var keyInfo = Console.ReadKey(true);

        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                MoveToPreviousItem();
                break;

            case ConsoleKey.DownArrow:
                MoveToNextItem();
                break;

            case ConsoleKey.Enter:
                SelectCurrentItem();
                break;

            case ConsoleKey.Escape:
                _exitRequested = true;
                _selectedIndex = -1;
                break;

            case ConsoleKey.D0 when ShowExitOption:
                _exitRequested = true;
                _selectedIndex = -1;
                break;

            default:
                // 处理数字键选择
                if (char.IsDigit(keyInfo.KeyChar))
                {
                    var number = int.Parse(keyInfo.KeyChar.ToString());
                    if (number == 0 && ShowExitOption)
                    {
                        _exitRequested = true;
                        _selectedIndex = -1;
                    }
                    else if (number >= 1 && number <= _menuItems.Count)
                    {
                        var targetIndex = number - 1;
                        if (_menuItems[targetIndex].IsEnabled && !_menuItems[targetIndex].IsSeparator)
                        {
                            _selectedIndex = targetIndex;
                            SelectCurrentItem();
                        }
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 移动到上一个可选项
    /// </summary>
    private void MoveToPreviousItem()
    {
        var totalItems = ShowExitOption ? _menuItems.Count + 1 : _menuItems.Count;
        var currentIndex = _selectedIndex;

        do
        {
            currentIndex = currentIndex <= 0 ? totalItems - 1 : currentIndex - 1;

            if (ShowExitOption && currentIndex == _menuItems.Count)
            {
                _selectedIndex = currentIndex;
                return;
            }

            if (currentIndex < _menuItems.Count && _menuItems[currentIndex].IsEnabled && !_menuItems[currentIndex].IsSeparator)
            {
                _selectedIndex = currentIndex;
                return;
            }
        } while (currentIndex != _selectedIndex);
    }

    /// <summary>
    /// 移动到下一个可选项
    /// </summary>
    private void MoveToNextItem()
    {
        var totalItems = ShowExitOption ? _menuItems.Count + 1 : _menuItems.Count;
        var currentIndex = _selectedIndex;

        do
        {
            currentIndex = (currentIndex + 1) % totalItems;

            if (ShowExitOption && currentIndex == _menuItems.Count)
            {
                _selectedIndex = currentIndex;
                return;
            }

            if (currentIndex < _menuItems.Count && _menuItems[currentIndex].IsEnabled && !_menuItems[currentIndex].IsSeparator)
            {
                _selectedIndex = currentIndex;
                return;
            }
        } while (currentIndex != _selectedIndex);
    }

    /// <summary>
    /// 选择当前项
    /// </summary>
    private void SelectCurrentItem()
    {
        if (ShowExitOption && _selectedIndex == _menuItems.Count)
        {
            _exitRequested = true;
            _selectedIndex = -1;
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < _menuItems.Count)
        {
            var item = _menuItems[_selectedIndex];
            if (item.IsEnabled && !item.IsSeparator)
            {
                _exitRequested = true;
            }
        }
    }

    /// <summary>
    /// 获取第一个可选项的索引
    /// </summary>
    /// <returns>第一个可选项的索引</returns>
    private int GetFirstEnabledIndex()
    {
        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (_menuItems[i].IsEnabled && !_menuItems[i].IsSeparator)
            {
                return i;
            }
        }
        return ShowExitOption ? _menuItems.Count : 0;
    }

    /// <summary>
    /// 菜单项
    /// </summary>
    private class MenuItem
    {
        public MenuItem(string text, Action? action, bool isEnabled)
        {
            Text = text;
            Action = action;
            IsEnabled = isEnabled;
        }

        public string Text { get; }
        public Action? Action { get; }
        public bool IsEnabled { get; }
        public bool IsSeparator { get; set; }
        public bool IsSubMenu { get; set; }
    }
}

/// <summary>
/// 快速菜单构建器
/// </summary>
public static class QuickMenu
{
    /// <summary>
    /// 创建简单选择菜单
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="options">选项</param>
    /// <returns>选择的选项索引，-1表示取消</returns>
    public static int Choose(string title, params string[] options)
    {
        var menu = new ConsoleMenu { Title = title };

        for (var i = 0; i < options.Length; i++)
        {
            var index = i; // 避免闭包问题
            menu.AddItem(options[i]);
        }

        return menu.Show();
    }

    /// <summary>
    /// 创建动作菜单
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="actions">动作字典</param>
    public static void Execute(string title, Dictionary<string, Action> actions)
    {
        var menu = new ConsoleMenu { Title = title };

        foreach (var action in actions)
        {
            menu.AddItem(action.Key, action.Value);
        }

        menu.ShowAndExecute();
    }

    /// <summary>
    /// 创建是/否菜单
    /// </summary>
    /// <param name="question">问题</param>
    /// <returns>true表示是，false表示否</returns>
    public static bool Confirm(string question)
    {
        var menu = new ConsoleMenu
        {
            Title = question,
            ShowExitOption = false
        };

        menu.AddItem("是");
        menu.AddItem("否");

        var result = menu.Show();
        return result == 0;
    }

    /// <summary>
    /// 创建分页菜单
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="title">标题</param>
    /// <param name="items">所有选项</param>
    /// <param name="pageSize">每页显示数量</param>
    /// <param name="formatter">格式化函数</param>
    /// <returns>选择的项目</returns>
    public static T? PagedChoose<T>(string title, List<T> items, int pageSize = 10, Func<T, string>? formatter = null)
    {
        if (items == null || items.Count == 0)
        {
            ConsoleColorWriter.WriteWarn("没有可选择的项目");
            return default;
        }

        formatter ??= item => item?.ToString() ?? "";
        var totalPages = (int)Math.Ceiling((double)items.Count / pageSize);
        var currentPage = 0;

        while (true)
        {
            var startIndex = currentPage * pageSize;
            var endIndex = Math.Min(startIndex + pageSize, items.Count);
            var pageItems = items.Skip(startIndex).Take(pageSize).ToList();

            var menu = new ConsoleMenu
            {
                Title = $"{title} (第 {currentPage + 1}/{totalPages} 页)",
                ShowExitOption = false
            };

            // 添加当前页的选项
            for (var i = 0; i < pageItems.Count; i++)
            {
                var item = pageItems[i];
                var actualIndex = startIndex + i;
                menu.AddItem($"{actualIndex + 1}. {formatter(item)}");
            }

            // 添加导航选项
            if (pageItems.Count > 0)
            {
                menu.AddSeparator();
            }

            if (currentPage > 0)
            {
                menu.AddItem("◄ 上一页");
            }

            if (currentPage < totalPages - 1)
            {
                menu.AddItem("下一页 ►");
            }

            menu.AddItem("取消");

            var selection = menu.Show();

            if (selection < pageItems.Count)
            {
                // 选择了具体项目
                return pageItems[selection];
            }

            // 处理导航
            var navigationStartIndex = pageItems.Count;
            var relativeIndex = selection - navigationStartIndex;

            if (currentPage > 0 && relativeIndex == 0)
            {
                // 上一页
                currentPage--;
            }
            else if (currentPage < totalPages - 1)
            {
                if ((currentPage > 0 && relativeIndex == 1) || (currentPage == 0 && relativeIndex == 0))
                {
                    // 下一页
                    currentPage++;
                }
                else
                {
                    // 取消
                    break;
                }
            }
            else
            {
                // 取消
                break;
            }
        }

        return default;
    }
}
