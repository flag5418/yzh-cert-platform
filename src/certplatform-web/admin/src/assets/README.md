# Assets - 项目资源目录

此目录存放当前项目的静态资源，包括表格/表单的 GridConfig JSON 配置。

## 目录结构

```
assets/
├── gridconfig/          # 项目级表格/表单配置 JSON
│   ├── sys_user_list.json
│   ├── sys_user_form.json
│   └── ...
└── README.md
```

## 优先级

项目级配置 > 核心库默认配置

当同一表名存在两份配置时，加载器优先使用项目级配置。

## 如何新增配置

1. 在 `gridconfig/` 下创建 `{tableName}_list.json`（列表）或 `{tableName}_form.json`（表单）
2. 前端通过 `/api/gridconfig/{tableName}` 获取配置
3. 后端会缓存 24 小时，修改后需重启或等待缓存过期

## GridConfig JSON 完整格式

```json
{
  "configName": "user_list",
  "tableName": "Sys_User",
  "floorFlag": false,
  "fillMode": "AutoFix",
  "columns": [
    {
      "row": 0,
      "rowSpan": 1,
      "col": 0,
      "colSpan": 1,
      "fieldName": "UserName",
      "desName": "用户名",
      "width": 120,
      "type": "TextBox",
      "yxk": false,
      "bcFlag": true,
      "xsFlag": true,
      "groupIndex": "0",
      "sxh": 0,
      "mrz": ""
    }
  ]
}
```
