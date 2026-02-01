#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandDescriptor
// Guid:05b2453e-2bce-4deb-aeb4-3fdb9cdb48c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.DevTools.CommandLine.Attributes;

namespace XiHan.Framework.DevTools.CommandLine.Commands;

/// <summary>
/// 命令描述符
/// </summary>
public class CommandDescriptor
{
    /// <summary>
    /// 创建命令描述符
    /// </summary>
    /// <param name="commandType">命令类型</param>
    public CommandDescriptor(Type commandType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));

        var commandAttr = commandType.GetCustomAttribute<CommandAttribute>() ??
            throw new ArgumentException($"类型 {commandType.Name} 没有 CommandAttribute 标记");

        Name = commandAttr.Name;
        Aliases = commandAttr.Aliases ?? [];
        Description = commandAttr.Description;
        IsDefault = commandAttr.IsDefault;
        Hidden = commandAttr.Hidden;
        Usage = commandAttr.Usage;

        // 解析选项和参数
        ParseMembers();
    }

    /// <summary>
    /// 命令名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 命令别名
    /// </summary>
    public string[] Aliases { get; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 是否为默认命令
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// 是否隐藏
    /// </summary>
    public bool Hidden { get; }

    /// <summary>
    /// 使用示例
    /// </summary>
    public string? Usage { get; }

    /// <summary>
    /// 命令类型
    /// </summary>
    public Type CommandType { get; }

    /// <summary>
    /// 选项描述符列表
    /// </summary>
    public List<OptionDescriptor> Options { get; } = [];

    /// <summary>
    /// 参数描述符列表
    /// </summary>
    public List<ArgumentDescriptor> Arguments { get; } = [];

    /// <summary>
    /// 子命令描述符列表
    /// </summary>
    public List<CommandDescriptor> SubCommands { get; } = [];

    /// <summary>
    /// 父命令
    /// </summary>
    public CommandDescriptor? Parent { get; set; }

    /// <summary>
    /// 获取完整的命令路径
    /// </summary>
    /// <returns>命令路径</returns>
    public string GetFullPath()
    {
        var parts = new List<string>();
        var current = this;

        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.Parent;
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 检查名称是否匹配
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>是否匹配</returns>
    public bool MatchesName(string name, bool ignoreCase = true)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (Name.Equals(name, comparison))
        {
            return true;
        }

        return Aliases.Any(alias => alias.Equals(name, comparison));
    }

    /// <summary>
    /// 查找子命令
    /// </summary>
    /// <param name="name">命令名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>子命令描述符</returns>
    public CommandDescriptor? FindSubCommand(string name, bool ignoreCase = true)
    {
        return SubCommands.FirstOrDefault(cmd => cmd.MatchesName(name, ignoreCase));
    }

    /// <summary>
    /// 获取所有可见的子命令
    /// </summary>
    /// <returns>可见子命令列表</returns>
    public List<CommandDescriptor> GetVisibleSubCommands()
    {
        return [.. SubCommands.Where(cmd => !cmd.Hidden)];
    }

    /// <summary>
    /// 创建命令实例
    /// </summary>
    /// <returns>命令实例</returns>
    public object CreateInstance()
    {
        return Activator.CreateInstance(CommandType) ?? throw new InvalidOperationException($"无法创建命令实例: {CommandType.Name}");
    }

    /// <summary>
    /// 解析类型成员
    /// </summary>
    private void ParseMembers()
    {
        var members = CommandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Cast<MemberInfo>()
            .Concat(CommandType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            .ToList();

        foreach (var member in members)
        {
            // 解析选项
            var optionAttr = member.GetCustomAttribute<CommandOptionAttribute>();
            if (optionAttr != null)
            {
                Options.Add(new OptionDescriptor(member, optionAttr));
            }

            // 解析参数
            var argumentAttr = member.GetCustomAttribute<CommandArgumentAttribute>();
            if (argumentAttr != null)
            {
                Arguments.Add(new ArgumentDescriptor(member, argumentAttr));
            }

            // 解析子命令
            var subCommandAttr = member.GetCustomAttribute<SubCommandAttribute>();
            if (subCommandAttr != null)
            {
                var subCommand = new CommandDescriptor(subCommandAttr.CommandType)
                {
                    Parent = this
                };
                SubCommands.Add(subCommand);
            }
        }

        // 按位置排序参数
        Arguments.Sort((a, b) => a.Position.CompareTo(b.Position));
    }
}
