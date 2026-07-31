# 认证平台前端页面开发指南 - 问题总结与最佳实践

> **版本**: v1.0.0  
> **日期**: 2026-07-31  
> **状态**: 已完成 Phase 2 基础设施重构

---

## 一、问题总结

### 1. 表单布局问题

#### 问题描述
- 表单中显示了 `code` 字段（业务编码），用户不应该看到
- 布局混乱：既有 `colSize: 12`（整行）又有 `colSize: 6`（半行）和 `colSize: 4`（1/3 行）
- 弹窗表单排列不整齐，用户体验差

#### 根本原因
1. **隐藏字段未正确配置**：`code` 字段没有设置 `type: 'hidden'`
2. **布局不统一**：没有遵循统一的列数规范
3. **缺少基类约束**：每个页面独立开发，没有统一标准

#### 解决方案
```javascript
// ✅ 正确做法：隐藏字段用 type: 'hidden'
const editFormOptions = [
  [
    { field: 'code', type: 'hidden' },      // 隐藏主键/编码
    { field: 'id', type: 'hidden' },        // 隐藏 ID
    { title: '名称', field: 'name', colSize: 12 }, // 整行
  ],
  [
    { title: '状态', field: 'status', colSize: 6 },   // 半行
    { title: '备注', field: 'notes', colSize: 6 },    // 半行
  ],
];
```

**布局规范**：
| 场景 | colSize | 说明 |
|------|---------|------|
| 隐藏字段 | 不设置或 type:'hidden' | code, id 等系统字段 |
| 整行显示 | 12 | 名称、备注等需要宽度的字段 |
| 2 列布局 | 6 | 标准 2 列（**推荐默认值**） |
| 3 列布局 | 4 | 特殊情况使用 |
| 4 列布局 | 3 | 短字段组合使用 |

---

### 2. 工具栏按钮问题

#### 问题描述
- 页面顶部有"自定义操作"按钮，不是标准 CRUD 按钮
- 缺少导入、导出、列设置、刷新等标准功能按钮
- 编辑和删除按钮在顶部工具栏，不符合操作习惯

#### 根本原因
1. **复制了错误的示例代码**：从 MES 示例复制了自定义按钮
2. **不理解 Vol 框架的标准按钮体系**
3. **缺少对用户操作习惯的考虑**

#### 解决方案
1. **移除自定义按钮**：删除 `<template #btnLeft>` 中的自定义内容
2. **使用框架标准按钮**：ViewGrid 自带新建、编辑、删除、导入、导出、刷新等按钮
3. **将编辑/删除移到行内操作列**：更符合用户直觉

**Vol 框架标准按钮**：
| 按钮 | 触发方式 | 说明 |
|------|----------|------|
| 新建 | 自动显示 | 打开新增弹窗 |
| 编辑 | 自动显示 | 编辑选中行 |
| 删除 | 自动显示 | 删除选中行 |
| 导入 | 需配置 | Excel 导入 |
| 导出 | 自动显示 | Excel 导出 |
| 刷新 | 自动显示 | 刷新列表 |
| 列设置 | 自动显示 | 显示/隐藏列 |

---

### 3. 数据刷新问题（重要！）

#### 问题描述
- 新增/编辑/删除后，整个列表刷新
- 如果用户在第 5 页添加数据，刷新后回到第 1 页
- 分页场景下体验极差

#### 根本原因
1. **使用了框架默认行为**：`proxy.search()` 会重新请求后端并重置分页
2. **不理解增量更新机制**：可以直接操作表格数据而不刷新

#### 解决方案
使用 **YZHBaseCrud** 的增量更新机制：

```javascript
// 新增成功后 - 在列表末尾添加一行
const handleAddAfter = (result, formData) => {
  if (result?.data) {
    gridRef.addRow(result.data); // 增量添加
  }
  return true; // 返回 false 可阻止框架默认刷新
};

// 编辑成功后 - 只更新当前行
const handleUpdateAfter = (result, formData) => {
  if (result?.data) {
    gridRef.updateRow(result.data); // 增量更新
  }
  return true;
};

// 删除成功后 - 从列表中移除对应行
const handleDelAfter = (result, rows) => {
  if (rows?.length > 0) {
    gridRef.removeRows(rows); // 增量移除
  }
  return true;
};
```

**增量更新优势**：
- ✅ 保持当前页码不变
- ✅ 保持滚动位置不变
- ✅ 减少网络请求
- ✅ 用户体验流畅

---

### 4. 操作按钮位置问题

#### 问题描述
- 编辑/删除按钮在顶部工具栏
- 需要先选中数据，再点击顶部按钮
- 操作流程不符合直觉

#### 根本原因
1. **传统桌面应用思维**：工具栏放全局操作
2. **Web 应用最佳实践缺失**：行内操作更直观

#### 解决方案
使用 **YZHBaseCrud** 的行内操作列：

```vue
<YZHBaseCrud
  :options="viewOptions"
  :enable-row-actions="true"
  :row-action-width="150"
/>
```

效果：每行末尾自动显示 [编辑] [删除] 按钮

**操作对比**：

| 方式 | 传统（顶部） | 行内（推荐） |
|------|-------------|-------------|
| 编辑流程 | 选中行 → 点击编辑按钮 | 直接点击行的编辑按钮 |
| 删除流程 | 选中行 → 点击删除按钮 → 确认 | 直接点击行的删除按钮 → 确认 |
| 用户认知 | 需要理解"先选后操作" | 直觉化"看到就能操作" |

---

## 二、开发流程总结

### 正确的开发流程

```
1. 创建 options.js（配置文件）
   ↓
2. 使用 YZHBaseCrud 创建 Vue 页面
   ↓
3. 配置路由（viewGird.js）
   ↓
4. 测试验证
```

### options.js 配置清单

```javascript
export default function () {
  // 1. table 配置
  const table = {
    name: "实体名",           // 与后端一致
    cnName: "中文显示名",
    url: "/api/前缀/",       // API 路径
    key: "Id",               // 主键字段
  };

  // 2. 表单字段（隐藏系统字段）
  const editFormFields = {
    id: "",                  // 主键
    code: "",                // 编码（隐藏）
    // ... 业务字段
  };

  // 3. 表单配置（统一 2 列布局）
  const editFormOptions = [
    [{ field: "id", type: "hidden" }],
    [{ title: "字段", field: "field", colSize: 6 }],
  ];

  // 4. 搜索配置
  const searchFormFields = { keyword: "" };
  const searchFormOptions = [[{ title: "搜索", field: "keyword" }]];

  // 5. 列配置（不需要操作列，基类自动添加）
  const columns = [
    { field: "id", hidden: true },
    { field: "name", title: "名称" },
  ];

  return { table, key, /* ... */ };
}
```

### Vue 页面模板

```vue
<template>
  <YZHBaseCrud
    :options="viewOptions"
    module-name="ModuleName"
    description="页面描述"
    @on-init="handleInit"
  />
</template>

<script setup>
import YZHBaseCrud from "@/components/yzh/YZHBaseCrud.vue";
import viewOptions from "./options.js";

const handleInit = ($vm) => {
  // 自定义初始化逻辑
};
</script>
```

---

## 三、常见问题速查

### Q1: 表单字段显示为空？
**A**: 检查 `editFormFields` 是否定义了该字段，且 `editFormOptions` 中有对应配置。

### Q2: 下拉框没有数据？
**A**: 
1. 确保 `dataKey` 与数据字典一致
2. 或在 `onInit` 中动态加载：`gridRef.getFormOption('field').data = [...]`

### Q3: 保存后报错？
**A**: 
1. 检查 `url` 是否与后端 Controller 路由匹配
2. 检查必填字段是否都有值
3. 查看浏览器控制台的网络请求

### Q4: 列表不显示数据？
**A**: 
1. 检查后端 API 是否返回数据
2. 检查 `columns` 的 `field` 是否与返回数据的字段名一致
3. 注意字段大小写（JS 区分大小写）

### Q5: 路由 404？
**A**: 
1. 检查 `viewGird.js` 中是否有对应路由
2. 检查 `path` 是否与数据库 `Sys_Menu.Url` 一致
3. 检查组件文件路径是否正确

---

## 四、文件变更记录

### 本次修复涉及的文件

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `components/yzh/YZHBaseCrud.vue` | **新增** | 基础 CRUD 窗体组件 |
| `components/yzh/YZHBaseCrud.jsx` | **新增** | 扩展兼容文件 |
| `components/yzh/README.md` | **新增** | 组件文档 |
| `views/cert/ISOStandard/ISOStandard.vue` | **修改** | 使用 YZHBaseCrud 重构 |
| `views/cert/ISOStandard/options.js` | **修改** | 修复布局问题 |
| `views/cert/AuditTask/AuditTask.vue` | **修改** | 使用 YZHBaseCrud 重构 |
| `views/cert/AuditTask/options.js` | **修改** | 修复布局问题 |

---

## 五、后续改进方向

### 短期（Phase 2 完成）
- [x] 创建 YZHBaseCrud 基类组件
- [x] 修复表单布局问题
- [x] 实现增量更新机制
- [x] 行内操作列支持
- [ ] 重构 CertificationBody 和 CertApplication 页面

### 中期（Phase 3）
- [ ] 创建 YZHAuditCrud 审核专用基类
- [ ] 创建 YZHConfigCrud 配置管理基类
- [ ] 实现批量操作支持
- [ ] 实现导入导出定制

### 长期（Phase 4+）
- [ ] 完善权限控制集成
- [ ] 实现操作日志记录
- [ ] 支持移动端适配

---

## 六、参考资料

- Vol 框架文档: http://v3.volcore.xyz/
- 项目规则: `docs/00-工程体系/项目全局规则.md`
- Vol Skill: `docs/60-AI工程设计/vol-skill.md`
- 故障排查: `docs/60-AI工程设计/vol-framework-troubleshooting.md`
