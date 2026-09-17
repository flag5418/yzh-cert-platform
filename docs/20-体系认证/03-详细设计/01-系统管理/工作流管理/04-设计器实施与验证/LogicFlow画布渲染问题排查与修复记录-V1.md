# LogicFlow v2 画布渲染坑与修复记录 - V1

> **日期**：2026-09-17 | **状态**：已验证（浏览器实测 + 引擎端到端回归通过）
> **关联**：
> - 问题来源：`NCConfig设计器-现状评审与完善建议-V1.md`（P0-2 画布布局塌陷 / 拖拽失效）
> - 实施依据：`工作流引擎迁移实施计划-V1.md`（已完成 9 阶段迁移）

---

## 一、问题现象

`/business/nc-config` 设计器选中规则后画布区域**空白**（或只剩边框），节点库拖拽到画布**无任何反应**；控制台零报错（或仅有被 try/catch 吞掉的异常）。同类问题也存在于 `report-rule-config` 页面。

## 二、根因（两个叠加问题）

### 根因 ①：LogicFlow v2 构造函数不创建 SVG

LogicFlow v2 在 `new LogicFlow({...})` 时**只创建包装容器**（`<div style="position:relative;width:100%;height:100%">`），**必须显式调用一次 `render()` 才会生成 SVG**。

原代码 `createLogicFlowInstance()` 构造后只绑事件 + ResizeObserver，从未调用 render。选中「无内容规则」走 `clearCanvas() + ensureStartNode()` 路径，两者都不触发 render → SVG 永远不存在 → `ensureStartNode` 往画布 addNode 实际写进了 store（footer 显示"节点: 1"）但无处渲染。

### 根因 ②：`background: '#fafbfc'` 字符串写法（真正的渲染杀手）

LogicFlow v2 的 `BackgroundConfig` 是**对象类型**。传字符串时，LF 内部把字符串当样式对象做 `Object.assign(style, background)` 展开，字符串被按字符索引枚举成 `{0:'#', 1:'f', 2:'a', ...}`，数字索引键进入 Preact style diff 后写入 SVG 元素的 `CSSStyleDeclaration`，触发 Chrome 原生限制：

```
TypeError: Failed to set an indexed property [0] on 'CSSStyleDeclaration':
Indexed property setter is not supported.
```

异常发生在 Preact 渲染循环内部，**整个 SVG 渲染中断且极难被业务代码捕获**（表现为"零报错但无 SVG"）。

隔离实验结论（`new LogicFlow` + `render` 空图）：

| background 写法 | 结果 |
|---|---|
| 不传 | ✅ SVG 正常 |
| `{ backgroundColor: '#fafbfc' }`（官方格式） | ✅ SVG 正常 |
| `'#fafbfc'`（字符串） | ❌ 必现 indexed property 异常 |

### 附：为什么不早发现

- 旧引擎评审时画布塌陷被归因为「容器 143px 过窄」（P0-2 前半段），布局修复后暴露出真正的渲染层问题。
- 历史上曾加过两层防御补丁（`logicflow-patch.ts` 引擎级拦截、`preact-shim.ts` vnode 清洗），但本异常的写入路径是 **Preact 内部对字符串的 Object.assign 展开**，绕过了 `h()`（shim 无效）且发生在引擎内部槽（原型拦截无效）。

## 三、修复方案（`designer.vue` / `report-rule-config/index.vue`）

```ts
diagram.value = new LogicFlow({
  container: canvasRef.value,
  grid: { size: 20, visible: true, type: 'mesh' },
  // ⚠️ LogicFlow v2 的 background 必须用对象形式（BackgroundConfig）。
  // 字符串形式会被 LF 内部按字符索引展开（{0:'#',1:'f'...}），
  // 数字键进入 Preact style diff 后触发 SVG CSSStyleDeclaration
  // 原生 indexed setter 异常，整个画布 SVG 渲染中断。
  background: { backgroundColor: '#fafbfc' },
  // ...
})

// LogicFlow v2：构造函数只创建包装容器，必须先 render() 一次才会生成 SVG。
// 否则选中无内容规则（clearCanvas + ensureStartNode 路径）时画布始终没有 SVG，
// 拖拽节点落点无法渲染。
try {
  diagram.value.render({ nodes: [], edges: [] })
} catch (e) {
  console.warn('[designer] 初始空图渲染失败（将由后续 resize/render 自愈）:', e)
}
```

同时：
- **移除** `preact-shim.ts` 及 `vite.config.ts` 中的 preact alias（无效防御，且接管整个 preact 有隐患）；
- **保留** `logicflow-patch.ts`（存量文件，`report-rule-config` 共用，无害）。

## 四、回归验证记录（2026-09-17 浏览器实测）

| 步骤 | 结果 |
|---|---|
| 选中规则「测试规则1」 | SVG + 网格 + 开始节点渲染 |
| 模拟拖拽「值比较」节点（dragstart/dragover/drop 全序列） | 节点 1 → 2，画布稳定 |
| 移除 preact-shim 后重复上述操作 | 通过（证明源头修复自足） |
| `vue-tsc` 检查 nc-config / report-rule-config | 零错误 |

## 五、引擎迁移验证（同日完成）

种子数据：`scripts/db/20260916_wf_skill_reflection_seed.sql` 已按**真实表结构**执行
（`wf_skill` 启用列为 `is_active`、双唯一键 `uk_code`+`uk_skill_code`；存量 compare/assemble 反射登记从旧程序集 `YZH.Core.Skills.*` 修正为新架构 `CertPlatform.Admin.Services.Workflow.Skills.*`）。

| 端点 | 场景 | 结果 |
|---|---|---|
| `POST /api/Workflow/test/node` | compare 反射 Skill（10 > 20） | ✅ `compare_result: false` |
| `POST /api/Workflow/test/node` | assemble 拼接 | ✅ `ISO13485-审核通过` |
| `POST /api/Workflow/test/ai-node` | AI 节点 + mockOutputs | ✅ camelCase 契约达标 |
| `POST /api/Workflow/test/run` | 完整三节点工作流 | ✅ `compare_result: true`，pathResults 完整 |
| 落库 | `wf_execution_task` / `wf_node_execution` | ✅ taskCode 精确对应，三节点全 completed |

> 序列化注解补充：`AiNodeTestResponse` / `AiNodeDebugInfo` 原缺 `JsonPropertyName`（响应为 PascalCase），已补齐 camelCase，与 `NodeTestResponse` / `TaskExecutionResponse` 契约一致（D-5）。

## 六、开发规范（新增）

1. **LogicFlow v2 初始化三件套**：构造 → `render(空图)` → 事件绑定。缺 render 则画布空壳。
2. **一切 LF 配置项中的"样式类"选项只用对象形式**（`background`、`style` 等），严禁字符串。
3. 若画布"无报错但空白"，优先怀疑渲染循环内部异常：全局挂 `window.addEventListener('error', ..., true)` 抓取后再定位。
4. 防御性补丁（shim/alias/原型拦截）只能兜底，不能替代源头正确性；引入前先做最小隔离实验证明其确实拦截了目标路径。
