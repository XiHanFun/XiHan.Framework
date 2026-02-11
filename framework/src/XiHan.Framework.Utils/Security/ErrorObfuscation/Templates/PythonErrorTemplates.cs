#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PythonErrorTemplates
// Guid:7e8f9a0b-2c3d-4e5f-6a7b-8c9d0e1f2a3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Python 错误模板
/// </summary>
internal static class PythonErrorTemplates
{
    /// <summary>
    /// Python 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "Traceback (most recent call last):\n  File \"/app/main.py\", line 45, in <module>\n    result = process_data(user_input)\n  File \"/app/processors/data.py\", line 89, in process_data\n    return data['user']['name']\nKeyError: 'user'",
        "Traceback (most recent call last):\n  File \"/usr/local/lib/python3.11/site-packages/flask/app.py\", line 2551, in __call__\n    return self.wsgi_app(environ, start_response)\n  File \"/app/views/home.py\", line 34, in index\n    users = User.query.all()\nAttributeError: 'NoneType' object has no attribute 'query'",
        "Traceback (most recent call last):\n  File \"/app/services/database.py\", line 67, in connect\n    conn = psycopg2.connect(**db_config)\n  File \"/usr/local/lib/python3.11/site-packages/psycopg2/__init__.py\", line 122, in connect\n    conn = _connect(dsn, connection_factory=connection_factory, **kwasync)\npsycopg2.OperationalError: could not connect to server: Connection refused",
        "Traceback (most recent call last):\n  File \"/app/api/endpoints.py\", line 156, in create_user\n    user_id = int(request.form['id'])\nValueError: invalid literal for int() with base 10: 'abc'",
        "Traceback (most recent call last):\n  File \"/app/utils/helpers.py\", line 203, in read_config\n    with open('/etc/app/config.yaml', 'r') as f:\nFileNotFoundError: [Errno 2] No such file or directory: '/etc/app/config.yaml'",
        "Traceback (most recent call last):\n  File \"/app/models/user.py\", line 78, in save\n    self.validate()\n  File \"/app/models/user.py\", line 45, in validate\n    assert self.email, 'Email is required'\nAssertionError: Email is required",
        "Traceback (most recent call last):\n  File \"/app/main.py\", line 23, in <module>\n    app.run(host='0.0.0.0', port=8080)\n  File \"/usr/local/lib/python3.11/site-packages/flask/app.py\", line 1098, in run\n    run_simple(t.cast(str, host), port, self, **options)\nOSError: [Errno 98] Address already in use",
        "Traceback (most recent call last):\n  File \"/app/workers/tasks.py\", line 134, in process_queue\n    data = json.loads(message)\n  File \"/usr/local/lib/python3.11/json/__init__.py\", line 346, in loads\n    return _default_decoder.decode(s)\njson.decoder.JSONDecodeError: Expecting value: line 1 column 1 (char 0)",
        "Traceback (most recent call last):\n  File \"/app/handlers/upload.py\", line 89, in handle_file\n    image = Image.open(file_path)\n  File \"/usr/local/lib/python3.11/site-packages/PIL/Image.py\", line 3227, in open\n    raise UnidentifiedImageError(msg)\nPIL.UnidentifiedImageError: cannot identify image file '/tmp/upload_xyz.dat'",
        "MemoryError: Unable to allocate 8.00 GiB for an array with shape (1000000000,) and data type float64"
    ];
}
