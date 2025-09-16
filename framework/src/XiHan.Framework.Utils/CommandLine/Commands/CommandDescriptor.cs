#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandDescriptor
// Guid:j6k7l891-i3k0-2617-gl5j-h7fd84c18143
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.CommandLine.Attributes;

namespace XiHan.Framework.Utils.CommandLine.Commands;

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
            var optionAttr = member.GetCustomAttribute<OptionAttribute>();
            if (optionAttr != null)
            {
                Options.Add(new OptionDescriptor(member, optionAttr));
            }

            // 解析参数
            var argumentAttr = member.GetCustomAttribute<ArgumentAttribute>();
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

/// <summary>
/// 选项描述符
/// </summary>
public class OptionDescriptor
{
    /// <summary>
    /// 创建选项描述符
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="optionAttr">选项属性</param>
    public OptionDescriptor(MemberInfo member, OptionAttribute optionAttr)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));

        LongName = optionAttr.LongName;
        ShortName = optionAttr.ShortName;
        Description = optionAttr.Description;
        Required = optionAttr.Required;
        DefaultValue = optionAttr.DefaultValue;
        IsSwitch = optionAttr.IsSwitch;
        AllowMultiple = optionAttr.AllowMultiple;
        MetaName = optionAttr.MetaName ?? GetDefaultMetaName();

        MemberType = member switch
        {
            PropertyInfo prop => prop.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new ArgumentException("成员必须是属性或字段")
        };

        // 自动检测布尔类型
        if (MemberType == typeof(bool) || MemberType == typeof(bool?))
        {
            IsSwitch = true;
        }
    }

    /// <summary>
    /// 长选项名称
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// 短选项名称
    /// </summary>
    public string? ShortName { get; }

    /// <summary>
    /// 选项描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// 是否为布尔开关
    /// </summary>
    public bool IsSwitch { get; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; }

    /// <summary>
    /// 参数名称（用于帮助显示）
    /// </summary>
    public string? MetaName { get; }

    /// <summary>
    /// 成员信息
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// 成员类型
    /// </summary>
    public Type MemberType { get; }

    /// <summary>
    /// 获取选项名称列表（包含长名称和短名称）
    /// </summary>
    /// <returns>选项名称列表</returns>
    public List<string> GetNames()
    {
        var names = new List<string> { LongName };
        if (!string.IsNullOrEmpty(ShortName))
        {
            names.Add(ShortName);
        }
        return names;
    }

    /// <summary>
    /// 获取默认的参数名称
    /// </summary>
    /// <returns>参数名称</returns>
    private string GetDefaultMetaName()
    {
        if (IsSwitch)
        {
            return "";
        }

        var typeName = MemberType.Name.ToLowerInvariant();
        return typeName switch
        {
            "string" => "VALUE",
            "int32" or "int64" or "int16" => "NUMBER",
            "double" or "single" or "decimal" => "NUMBER",
            "datetime" => "DATE",
            "boolean" => "",
            _ => "VALUE"
        };
    }
}

/// <summary>
/// 参数描述符
/// </summary>
public class ArgumentDescriptor
{
    /// <summary>
    /// 创建参数描述符
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="argumentAttr">参数属性</param>
    public ArgumentDescriptor(MemberInfo member, ArgumentAttribute argumentAttr)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));

        Position = argumentAttr.Position;
        Name = argumentAttr.Name;
        Description = argumentAttr.Description;
        Required = argumentAttr.Required;
        DefaultValue = argumentAttr.DefaultValue;
        AllowMultiple = argumentAttr.AllowMultiple;

        MemberType = member switch
        {
            PropertyInfo prop => prop.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new ArgumentException("成员必须是属性或字段")
        };
    }

    /// <summary>
    /// 参数位置
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; }

    /// <summary>
    /// 成员信息
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// 成员类型
    /// </summary>
    public Type MemberType { get; }
}
