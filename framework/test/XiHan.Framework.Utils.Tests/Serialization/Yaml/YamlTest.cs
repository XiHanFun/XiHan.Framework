// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
