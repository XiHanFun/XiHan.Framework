#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SftpConnectionInfo
// Guid:fba078fc-bcde-4ab3-807f-92bd460501ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 15:34:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Net.Sftp;

/// <summary>
/// SFTP连接信息
/// </summary>
public class SftpConnectionInfo
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SftpConnectionInfo()
    {
    }

    /// <summary>
    /// 构造函数（使用密码认证）
    /// </summary>
    /// <param name="host">主机名或IP地址</param>
    /// <param name="port">端口号</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    public SftpConnectionInfo(string host, int port, string username, string password)
    {
        Host = host;
        Port = port;
        Username = username;
        Password = password;
    }

    /// <summary>
    /// 构造函数（使用密钥认证）
    /// </summary>
    /// <param name="host">主机名或IP地址</param>
    /// <param name="port">端口号</param>
    /// <param name="username">用户名</param>
    /// <param name="privateKeyPath">私钥文件路径</param>
    /// <param name="privateKeyPassphrase">私钥密码</param>
    public SftpConnectionInfo(string host, int port, string username, string privateKeyPath, string privateKeyPassphrase = "")
    {
        Host = host;
        Port = port;
        Username = username;
        PrivateKeyPath = privateKeyPath;
        PrivateKeyPassphrase = privateKeyPassphrase;
    }

    /// <summary>
    /// 主机名或IP地址
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// 端口号，默认为22
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// 私钥文件路径
    /// </summary>
    public string PrivateKeyPath { get; set; } = "";

    /// <summary>
    /// 私钥密码
    /// </summary>
    public string PrivateKeyPassphrase { get; set; } = "";

    /// <summary>
    /// 连接超时时间(毫秒)，默认30秒
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30000;
}
