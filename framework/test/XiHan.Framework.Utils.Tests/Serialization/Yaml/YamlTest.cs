#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlTest
// Guid:beb9cbd3-7cdd-4134-b7e3-f7f2dd252b0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/25 20:34:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlTest
/// </summary>
public class YamlTest
{
    public string JsonString = """
        {
            "Name": "ZhaiFanhua",
            "Age": 18,
            "Hobby": [
                "Coding",
                "Reading",
                "Gaming"
            ],
            "IsStudent": false,
            "Address": {
                "City": "Shanghai",
                "Country": "China"
            }
        }
        """;

    public string YamlString = """
        Name: ZhaiFanhua
        Age: 18
        Hobby:
          - Coding
          - Reading
          - Gaming
        IsStudent: false
        Address:
          City: Shanghai
          Country: China
        """;

    public string ToYamlString()
    {
        return JsonString.ToYaml();
    }

    public string ToJsonString()
    {
        return YamlString.ToJson();
    }
}
