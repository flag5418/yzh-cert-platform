# 2026-08-07 EF Core Column 映射 snake_case 导致 HTTP 400 + 表格不显示

> **背景**：配置驱动 UI 架构上线后，点击「刷新配置」按钮报 `Unknown column 'y.CheckboxSelection'` 错误，表格数据无法显示。

> **官方文档参考**：
> - 数据库访问（EF Core/repository）：http://v3.volcore.xyz/docs/cs/dev/db.html
> - 接口返回大小写（JsonNormal）：http://v3.volcore.xyz/docs/cs/dev/case.html
> - 前端 API 传参：http://v3.volcore.xyz/docs/cs/dev/api.html

---

## 📝 问题索引

| 编号 | 模块 | 问题现象 | 优先级 |
|------|------|----------|--------|
| P4-01 | 后端 EF Core | `GET /api/yzh-page-config/all` 返回 400 Bad Request | P0 |
| P4-02 | 前端表格 | Store 刷新失败 → 配置为空 → options.js 回退 → 表格无列 | P0 |
| P4-03 | 前端 UI | 顶部「刷新配置」按钮颜色与工具栏其他元素不一致 | P2 |

---

## 1️⃣ P4-01 Unknown column 'y.CheckboxSelection' in 'field list'

### 问题现象

```
GET http://localhost:9991/api/yzh-page-config/all 400 (Bad Request)

后端日志：
MySqlException: Unknown column 'y.CheckboxSelection' in 'field list'
```

### 根因分析

**EF Core 属性名与数据库列名风格不一致**。

> **官方文档对照**：[db.html](http://v3.volcore.xyz/docs/cs/dev/db.html) 中所有 `repository.FindAsIQueryable(x => 条件)` 示例均直接使用 C# 属性名（PascalCase），如 `x.CreateDate`、`x.User_Id`。EF Core 默认将属性名直接作为 SQL 列名，**不自动转换命名风格**。Vol 框架内置表（`Sys_Menu`、`Sys_User` 等）全部使用 PascalCase 列名，与属性名一致。

| 维度 | C# 实体属性 | 数据库列 |
|------|------------|---------|
| 风格 | PascalCase | snake_case |
| 示例 | `CheckboxSelection` | `checkbox_selection` |
| 示例 | `PageKey` | `page_key` |
| 示例 | `ColumnSxh` | `column_sxh` |

EF Core 默认行为：**按 C# 属性名生成 SQL 列名**，不会自动将 PascalCase 转换为 snake_case。

```csharp
// 实体定义（修复前）
public class YzhPageConfig : BaseEntity
{
    public byte CheckboxSelection { get; set; } = 1;  // ← EF 生成 SQL: y.CheckboxSelection
    // ...
}

// EF 生成的 SQL（错误）
SELECT y.Id, y.PageKey, ..., y.CheckboxSelection, ...  // ← checkbox_selection 不存在！
FROM yzh_page_config AS y
WHERE y.is_active = 1
```

### 影响范围

以下两个实体的所有属性都需要添加 `[Column]` 特性：

1. **`YzhPageConfig.cs`** — 23 个属性（`page_key`, `page_title`, `checkbox_selection`, ...）
2. **`YzhFieldConfig.cs`** — 30+ 个属性（`field_name`, `xs_flag`, `column_sxh`, `control_type`, ...）

### 解决方案

给每个实体属性显式添加 `[Column("snake_case_name")]` 特性：

```csharp
using System.ComponentModel.DataAnnotations.Schema;

[Table("yzh_page_config")]
public class YzhPageConfig : BaseEntity
{
    [Column("page_key")]
    [Required]
    [StringLength(50)]
    public string PageKey { get; set; }

    [Column("checkbox_selection")]
    public byte CheckboxSelection { get; set; } = 1;

    // ... 所有属性都加 [Column] 特性
}
```

### 修复文件

- 📄 `vol.api/VOL.Entity/CertPlatform/Sys/YzhPageConfig.cs`
- 📄 `vol.api/VOL.Entity/CertPlatform/Sys/YzhFieldConfig.cs`

### 预防措施

> **规则**: 当数据库表使用 snake_case 命名时，C# 实体属性**必须**添加 `[Column]` 特性。

> **与 PascalCase 策略的关系**：本项目 Cert 业务表采用 PascalCase 列名（见 [Phase2 联调 P2-01](./2026-08-03_Phase2联调全栈问题修复记录.md)），**不需要** `[Column]` 特性。但 YZH 配置表（`yzh_*` 系列）使用 snake_case 列名，**必须**加 `[Column]`。两种策略在同一项目中共存，按表前缀区分即可。

1. **建表时同步创建实体**：写完 DDL 后立即创建 Entity 类，逐字段加 `[Column]`
2. **代码审查检查项**：新实体是否所有属性都有 `[Column]` 映射
3. **考虑全局约定**（远期）：在 EF Core 的 `OnModelCreating` 中配置全局 snake_case 约定
   ```csharp
   // 远期优化：在 DbContext 中统一配置
   modelBuilder.Model.GetEntityTypes().ForEach(entity =>
   {
       entity.GetProperties().Where(p => !p.IsPrimaryKey()).ForEach(prop =>
       {
           prop.SetColumnName(SnakeCase(prop.Name));  // 需要引入 Humanizer 库
       });
   });
   ```
4. **快速诊断命令**：
   ```bash
   # 对比实体属性名与数据库列名
   docker exec yzh-mysql mysql -u root -p'Yzh123456.' yzh_cert_platform \
     -e "SHOW COLUMNS FROM yzh_page_config;" | grep _
   # 如果输出含 snake_case 列名，对应实体必须有 [Column] 特性
   ```

---

## 2️⃣ P4-02 表格不显示数据

### 问题现象

1. 点击「刷新配置」→ 400 错误
2. ISOStandard / CertificationBody 页面表格为空
3. 控制台日志显示 `loadData 完成: 22 个字段`，但无列渲染

### 根因分析

**链式故障**：

```
后端 400 错误 (P4-01)
    ↓
Store.refresh() 失败 → state.configs = {}
    ↓
YZHConfigLoader.loadPageConfig() → Store 未命中 → API 降级也失败
    ↓
dbFieldConfigs.value = [] （空数组）
    ↓
buildColumnsFromDbConfig() → return []
    ↓
columns computed → 回退到 options.js.columns（也是空数组）
    ↓
表格无列定义 → 不显示任何数据
```

### 解决方案

修复 P4-01 后自动解决。

---

## 3️⃣ P4-03 按钮颜色不一致

### 问题现象

顶部工具栏的「刷新配置」按钮使用 `<el-button link>` 样式（蓝色文字），而旁边的菜单筛选、消息等元素使用 `<a>` 标签样式（灰色图标 + 文字），视觉上不协调。

### 解决方案

改为与其他工具栏元素一致的 `<a>` + `<i>` 图标风格：

```html
<!-- 修复前 -->
<el-button link size="small" :loading="loading">
  <RefreshRight /> 配置
</el-button>

<!-- 修复后 -->
<a :style="{ opacity: loading ? 0.6 : 1, cursor: loading ? 'not-allowed' : 'pointer' }">
  <i :class="loading ? 'el-icon-loading' : 'icon icon-refresh'"></i>
  <span>配置</span>
  <span v-if="version">v{{ version }}</span>
</a>
```

---

## 关键经验总结

| 经验 | 说明 | 官方文档参考 |
|------|------|-------------|
| **snake_case 是坑** | Vol 框架原有表用 PascalCase 列名，但 YZH 新表用了 snake_case，EF Core 默认不转换 | [db.html](http://v3.volcore.xyz/docs/cs/dev/db.html) 全部示例使用 PascalCase |
| **链式故障排查** | 一个后端错误可能导致前端多层回退失败，需从根因修复 | - |
| **UI 一致性** | 工具栏元素应使用统一的 `<a>` 标签风格，不要混用 `el-button link` | - |
| **Column 特性是必须品** | 不是可选项，只要列名和属性名不同就必须加 | EF Core 官方约定 |
| **接口返回大小写** | `JsonNormal()` 保持原始大小写，`Json()` 转小驼峰 | [case.html](http://v3.volcore.xyz/docs/cs/dev/case.html) |
