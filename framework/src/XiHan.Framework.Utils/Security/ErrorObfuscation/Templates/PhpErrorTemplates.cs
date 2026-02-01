#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PhpErrorTemplates
// Guid:5c6d7e8f-0a1b-2c3d-4e5f-6a7b8c9d0e1f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// PHP 错误模板
/// </summary>
internal static class PhpErrorTemplates
{
    /// <summary>
    /// PHP 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "Fatal error: Uncaught Error: Call to undefined function mysql_connect() in /var/www/html/includes/database.php:15\nStack trace:\n#0 /var/www/html/index.php(23): Database->connect()\n#1 {main}\n  thrown in /var/www/html/includes/database.php on line 15",
        "Warning: mysqli::query(): MySQL server has gone away in /var/www/html/classes/DatabaseHandler.php on line 89\n\nFatal error: Uncaught Exception: Connection lost in /var/www/html/classes/DatabaseHandler.php:92\nStack trace:\n#0 /var/www/html/controllers/UserController.php(45): DatabaseHandler->query('SELECT * FROM u...')\n#1 {main}",
        "Parse error: syntax error, unexpected 'echo' (T_ECHO), expecting ')' in /var/www/html/views/home.php on line 67",
        "Fatal error: Maximum execution time of 30 seconds exceeded in /var/www/html/processor/DataProcessor.php on line 156",
        "Notice: Undefined variable: user_data in /var/www/html/controllers/ProfileController.php on line 34\n\nWarning: Invalid argument supplied for foreach() in /var/www/html/controllers/ProfileController.php on line 35",
        "Fatal error: Allowed memory size of 134217728 bytes exhausted (tried to allocate 65536 bytes) in /var/www/html/services/ImageProcessor.php on line 203",
        "Warning: file_get_contents(/etc/app/config.ini): failed to open stream: Permission denied in /var/www/html/config/ConfigLoader.php on line 28",
        "Fatal error: Uncaught PDOException: SQLSTATE[HY000] [2002] Connection refused in /var/www/html/database/Connection.php:42\nStack trace:\n#0 /var/www/html/database/Connection.php(42): PDO->__construct('mysql:host=loca...')\n#1 /var/www/html/index.php(18): Connection->connect()\n#2 {main}"
    ];
}
