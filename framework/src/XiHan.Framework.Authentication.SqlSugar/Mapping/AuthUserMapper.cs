// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.Users;

namespace XiHan.Framework.Authentication.SqlSugar.Mapping;

/// <summary>
/// 用户信息与用户实体的双向映射
/// </summary>
public static class AuthUserMapper
{
    /// <summary>
    /// 规范化用户名
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <returns>按不变区域转为大写的用户名</returns>
    public static string NormalizeUserName(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);

        return userName.ToUpperInvariant();
    }

    /// <summary>
    /// 把用户信息映射为实体
    /// </summary>
    /// <remarks>
    /// 忽略 <see cref="UserInfo.UserId"/>，主键取 <paramref name="basicId"/>。
    /// </remarks>
    /// <param name="user">用户信息</param>
    /// <param name="basicId">用户标识</param>
    /// <param name="tenantId">租户标识</param>
    /// <returns>用户实体</returns>
    public static SysAuthUser ToEntity(UserInfo user, long basicId, long tenantId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new SysAuthUser(basicId)
        {
            TenantId = tenantId,
            UserName = user.Username,
            NormalizedUserName = NormalizeUserName(user.Username),
            PasswordHash = user.PasswordHash,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            TwoFactorEnabled = user.TwoFactorEnabled,
            TwoFactorSecret = user.TwoFactorSecret,
            RecoveryCodes = SerializeRecoveryCodes(user.RecoveryCodes),
            IsLocked = user.IsLocked,
            LockoutEnd = StorageTime.ToUtc(user.LockoutEnd),
            FailedLoginAttempts = user.FailedLoginAttempts,
            LastLoginTime = StorageTime.ToUtc(user.LastLoginTime),
            PasswordChangedTime = StorageTime.ToUtc(user.PasswordChangedTime),
            IsActive = user.IsActive,
            AdditionalData = SerializeAdditionalData(user.AdditionalData)
        };
    }

    /// <summary>
    /// 把实体映射为用户信息
    /// </summary>
    /// <param name="entity">用户实体</param>
    /// <returns>用户信息</returns>
    public static UserInfo ToUserInfo(SysAuthUser entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new UserInfo
        {
            UserId = entity.BasicId.ToString(CultureInfo.InvariantCulture),
            Username = entity.UserName,
            PasswordHash = entity.PasswordHash,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            TwoFactorEnabled = entity.TwoFactorEnabled,
            TwoFactorSecret = entity.TwoFactorSecret,
            RecoveryCodes = DeserializeRecoveryCodes(entity.RecoveryCodes),
            IsLocked = entity.IsLocked,
            LockoutEnd = StorageTime.FromStorage(entity.LockoutEnd),
            FailedLoginAttempts = entity.FailedLoginAttempts,
            LastLoginTime = StorageTime.FromStorage(entity.LastLoginTime),
            PasswordChangedTime = StorageTime.FromStorage(entity.PasswordChangedTime),
            IsActive = entity.IsActive,
            AdditionalData = DeserializeAdditionalData(entity.AdditionalData)
        };
    }

    /// <summary>
    /// 序列化恢复码
    /// </summary>
    /// <param name="recoveryCodes">恢复码哈希列表</param>
    /// <returns>JSON 数组，输入为空时返回空数组</returns>
    public static string SerializeRecoveryCodes(List<string>? recoveryCodes)
    {
        return JsonSerializer.Serialize(recoveryCodes ?? []);
    }

    /// <summary>
    /// 反序列化恢复码
    /// </summary>
    /// <param name="json">JSON 数组</param>
    /// <returns>恢复码哈希列表，输入为空白时返回空列表</returns>
    public static List<string> DeserializeRecoveryCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    /// <summary>
    /// 序列化附加数据
    /// </summary>
    /// <param name="additionalData">附加数据</param>
    /// <returns>JSON 对象，输入为空时返回空</returns>
    public static string? SerializeAdditionalData(Dictionary<string, object>? additionalData)
    {
        return additionalData is null ? null : JsonSerializer.Serialize(additionalData);
    }

    /// <summary>
    /// 反序列化附加数据
    /// </summary>
    /// <remarks>
    /// 值以 <see cref="JsonElement"/> 返回。
    /// </remarks>
    /// <param name="json">JSON 对象</param>
    /// <returns>附加数据，输入为空白时返回空</returns>
    public static Dictionary<string, object>? DeserializeAdditionalData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }
}
