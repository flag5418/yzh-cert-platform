# Assets - 核心默认资源目录

此目录存放 yzh.vue.core 核心库的默认静态资源。

## 目录结构

```
assets/
├── gridconfig/          # 默认表格/表单配置 JSON
└── README.md
```

## 设计原则

1. **核心默认**：此目录提供组件的 fallback 配置，当项目未覆盖时使用
2. **项目可覆盖**：业务项目在 `src/assets/gridconfig/` 放置同名文件即可覆盖
3. **运行时加载**：配置通过 HTTP API (`/api/gridconfig/{name}`) 按需加载

## GridConfig JSON 格式

```json
{
  "configName": "user_list",
  "tableName": "Sys_User",
  "columns": [
    {
      "fieldName": "UserName",
      "desName": "用户名",
      "width": 120,
      "xsFlag": true
    }
  ]
}
```
