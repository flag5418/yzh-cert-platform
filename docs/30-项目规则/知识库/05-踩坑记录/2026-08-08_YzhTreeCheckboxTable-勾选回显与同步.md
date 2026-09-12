# YzhTreeCheckboxTable 勾选回显与同步踩坑记录

**日期**: 2026-08-08
**页面**: OrgStandard（机构-标准关联）
**组件**: `YzhTreeCheckboxTable.vue`
**耗时**: 约 4 小时（应该 30 分钟解决的问题）

> **官方文档参考**：
> - 数据库访问（repository/EF Core/跨库访问）：http://v3.volcore.xyz/docs/cs/dev/db.html
> - Service 业务扩展（Add/Update/Del 钩子）：http://v3.volcore.xyz/docs/cs/service/guid.html
> - Element Plus el-table toggleRowSelection API：https://element-plus.org/zh-CN/component/table.html#methods

---

## 问题现象

1. **取消勾选报错**：`Cannot create a DbSet for 'CertOrgStandard' because this type is not included in the model for the context.`
2. **勾选后没有保存成功**：前端显示差异计算正常，但后端 API 没有被调用或返回错误

---

## 根因分析（3 个问题叠加）

### 问题 1：Vite 编译语法错误（隐藏最深）

**现象**: `[vue/compiler-sfc] Unexpected token (393:1)` 或 `(534:0)`

**根因**: 文件中存在多余的 `}` 括号，导致 Vue SFC 编译失败。但错误信息指向的行号不准确，难以定位。

**解决**: 完全重写文件，确保括号正确匹配。

**教训**:
- 当 Vite 报错行号和实际不一致时，直接重写文件比逐行排查更快
- 使用 `vue-tsc --noEmit` 可以获得更准确的错误位置

---

### 问题 2：程序化设置 checkbox 触发误同步（核心逻辑 bug）

**现象**: 页面加载时自动调用 Sync API（不应该），用户手动操作却不触发

**根因**: `syncCheckboxes()` 调用 `toggleRowSelection(row, true, true)` 时：
- 第三个参数 `emitEvent=true` 触发了 `selection-change` 事件
- `handleSelectionChange` 计算差异 → 调用 `debouncedSync()` → 调用后端 API
- 这是**程序化设置选中状态**，不是用户操作，不应该触发同步！

**解决方案**: 添加 `isSettingCheckboxes` 标志位

```typescript
// syncCheckboxes 中
isSettingCheckboxes = true
// ... toggleRowSelection 操作 ...
setTimeout(() => { isSettingCheckboxes = false }, 200)

// handleSelectionChange 开头
if (isSettingCheckboxes) return  // 跳过程序化触发的变更
```

**关键代码位置**:
- `YzhTreeCheckboxTable.vue:204` — 标志位声明
- `YzhTreeCheckboxTable.vue:374` — 设置标志位
- `YzhTreeCheckboxTable.vue:402` — 检查标志位

---

### 问题 3：EF Core 实体未注册到 DbContext

**现象**: 后端返回 500 错误，`Cannot create a DbSet for 'CertOrgStandard'`

**根因**: `CertOrgStandard` 实体类存在，但没有在 EF Core 的 `DbContext.OnModelCreating` 中注册为 `DbSet`。

> **官方文档对照**：[db.html](http://v3.volcore.xyz/docs/cs/dev/db.html) 「跨业务类库访问其他表」章节提供了两种方式：
> 1. **EF 原生方式**：`DBServerProvider.GetEFDbContext<表>()` 获取 DbContext，再 `dbContext.Set<表>()` 操作
> 2. **Repository 注入**：在 Service 构造函数中注入其他表的 Repository（需 `[ActivatorUtilitiesConstructor]` 标记）
>
> 两种方式都要求实体被 EF Core 扫描到。Vol 框架通过 `[Entity]` 属性 + `BaseEntity` 继承实现自动扫描（见 [关联表保存问题修复](./2026-08-08_关联表保存问题与T+V模式修复.md)）。

**解决方案**: 将所有 `_db.Set<CertOrgStandard>()` 操作改为原生 SQL：

```csharp
// ❌ 错误：使用 EF Core DbSet
_db.Set<CertOrgStandard>().Where(...)

// ✅ 正确：使用原生 SQL
var connection = _db.Database.GetDbConnection();
var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT * FROM cert_org_standard WHERE ...";
```

**注意**: `GetOrgStdIds` 方法已经使用了原生 SQL（有注释说明原因），但 `SyncOrgStandards` 方法遗漏了。

---

## 修改文件清单

### 前端（1 个文件）

| 文件 | 修改内容 |
|------|----------|
| `src/yzh/components/YzhTreeCheckboxTable.vue` | 1. 重写整个文件修复语法错误<br>2. 添加 `isSettingCheckboxes` 标志位<br>3. 清理调试日志<br>4. ID 类型统一为 `string` |

### 后端（1 个文件）

| 文件 | 修改内容 |
|------|----------|
| `Controllers/CertPlatform/OrgLinkController.cs` | `SyncOrgStandards` 方法从 EF Core 改为原生 SQL |

---

## 经验总结

### 1. el-table toggleRowSelection 的陷阱

```javascript
// ⚠️ 第三参数 emitEvent=true 会触发 @selection-change
tableRef.toggleRowSelection(row, true, true)  // 会触发事件

// ✅ 如果只是 UI 更新，不需要触发事件
tableRef.toggleRowSelection(row, true)  // 不触发事件
```

### 2. 程序化操作 vs 用户操作的区分模式

当组件需要"内部设置状态"时，必须使用标志位区分：
```typescript
let isInternalOperation = false

function internalUpdate() {
  isInternalOperation = true
  // ... 修改状态 ...
  setTimeout(() => { isInternalOperation = false }, 100)
}

function onUserAction() {
  if (isInternalOperation) return  // 忽略程序化触发的事件
  // ... 处理用户操作 ...
}
```

### 3. EF Core 未注册实体的快速判断

如果看到错误 `Cannot create a DbSet for 'Xxx'`：
1. 检查 `DbContext` 是否有 `public DbSet<Xxx> Xxxs { get; set; }`
2. 如果没有，要么注册实体，要么改用原生 SQL
3. 对于中间表/关联表，通常不需要注册为实体，用原生 SQL 更简单

> **官方文档对照**：[db.html](http://v3.volcore.xyz/docs/cs/dev/db.html) 提供了 `DBServerProvider.GetEFDbContext<表>()` 获取跨库 DbContext 的方式，但前提是实体已注册。如果实体未注册，可使用 `repository.DapperContext` 或 `DBServerProvider.SqlDapper` 执行原生 SQL。

### 4. 调试日志的添加策略

**正确做法**:
- 只在关键决策点添加日志（如 `doSync` 入口、`handleSelectionChange` 入口）
- 日志要有明确的标识前缀 `[ComponentName]`
- 问题解决后**立即清理**调试日志

**本次错误**: 添加了过多日志（每行一个 console.log），反而干扰了问题定位

---

## 防范措施

1. **新组件开发 checklist**:
   - [ ] 确认 EF Core 实体已注册或使用原生 SQL
   - [ ] 程序化操作添加标志位防止事件冒泡
   - [ ] toggleRowSelection 注意 emitEvent 参数
   - [ ] 调试日志控制在 5 条以内

2. **Code Review 要点**:
   - [ ] 检查是否有未注册实体的 DbSet 操作
   - [ ] 检查事件处理是否有防重复触发机制
   - [ ] 检查括号匹配（特别是复杂逻辑）
