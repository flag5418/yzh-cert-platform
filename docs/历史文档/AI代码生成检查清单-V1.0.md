---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_ai_checklist
    ContentPropagator: 001191440300708461136TXGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_ai_checklist
---

# AI 代码生成检查清单

> **版本**：V1.0 | **日期**：2026-07-31 | **状态**：成熟态
>
> **定位**：AI 在生成任何代码前必须逐项检查的清单。每次对话只需执行一次。

---

## 一、对话启动检查

```
□ Step 1: 加载项目全局规则
  → 已读取 项目全局规则.md（仓库根目录）
  → 已确认当前版本号：V1.2

□ Step 2: 判断需求类型
  → 代码需求 → 已加载 vol-skill.md 的 §12.Z
  → 架构需求 → 已加载 20-架构决策/README.md
  → 文档需求 → 已加载对应文件夹的 README.md

□ Step 3: 确认边界
  → 已读取 60-AI工程设计/YZH-知识库/03-边界与约束.md
  → 已确认哪些文件不可修改

□ Step 4: 开始执行
```

---

## 二、代码生成检查

### 2.1 通用检查

```
□ 目标文件夹的 README 已查阅
□ 需求对应的 §12 章节已定位（在输出中引用 §12.X.Y）
□ YZH 组件的代码就绪度已确认
□ 03-边界约束未被违反
□ 输出格式符合用户偏好（直接给文件路径 + 代码块）
```

### 2.2 后端代码检查

```
□ 使用 Partial Service（非自动生成的 Service）
□ 钩子选择正确（AddOnExecuted/AuditOnExecuted 等）
□ 数据库访问使用 EF LINQ + repository（非 EFsql）
□ 分页查询使用 TakePage(Page, Rows)
□ 自定义接口返回 JsonNormal(...)
□ 未修改 Vol 源码文件（9 个不可修改）
□ 未调用未实现的 YZH 组件
```

### 2.3 前端代码检查

```
□ 使用 {表}.vue（非 Edit.vue，除非是独立页编辑）
□ Props Hook 使用正确（searchBefore/addBefore 等）
□ 字段事件使用 onInit/onInited + getFormOption
□ 未修改 options.js
□ 未修改 .jsx 业务文件
□ 未修改 ViewGrid 源码
```

### 2.4 文档更新检查

```
□ 文件头包含版本号、日期、状态
□ 与其他文档无矛盾
□ 量化结论有数据支撑
□ 对齐项目全局规则（命名、路径、技术栈）
□ 足够详细，可直接指导开发
```

---

## 三、YZH 组件就绪度速查

| 组件 | 可直接调用 | 替代方案 |
|------|-----------|---------|
| YZHBaseEntity | ✅ 是 | - |
| YZHModule | ⚠️ 仅注册 | - |
| YZHAuditedAttribute | ❌ 否 | 使用 Vol 原生钩子 |
| ICodeRule | ❌ 否 | 手动生成编码 |
| YZHValidationAttribute | ❌ 否 | 使用 Vol 原生验证 |
| IDeleteStrategy | ❌ 否 | 使用 Vol 原生删除 |
| YZHServiceBase | ❌ 否 | 使用 Vol 原生 ServiceBase |
| YZHControllerBase | ❌ 否 | 使用 Vol 原生控制器 |

---

## 四、快速路由表

| 需求 | 跳转 | 文件 |
|------|------|------|
| 查不到数据/保存写库/审核后写关联表 | §12.A.0 + §12.A.7 | Partial Service |
| 表单字段联动/onChange/onInit | §12.E.1 | `{表}.vue` |
| 弹框选数/复杂 UI | §12.J | `{表}.vue` + 子组件 |
| 独立页新建编辑 | §12.K | `Edit.vue` |
| 自定义 API/数据库查询 | §12.B.0 + §12.J.10 | Partial 三件套 |
| 新增数据库字段 | 02-YZH增量清单 §3 | Entity + EF |

---

## 五、违规后果速查

| 违规行为 | 后果 |
|---------|------|
| 跳过加载项目全局规则 | 产出不符合项目约束的代码 |
| 直接修改 Vol 源码 | Vol 升级时 YZH 全部编译失败 |
| 调用未实现的 YZH 组件 | 编译错误 |
| 用 EFsql 替代 EF LINQ | 绕过钩子体系，审计/权限过滤失效 |
| 输出长篇解释 | 违反用户偏好 |

---

**文档版本**：V1.0
**创建时间**：2026-07-31
**创建背景**：整合所有约束为可执行清单，确保 AI 每次对话都对齐项目要求
*（内容由AI生成，仅供参考）*
