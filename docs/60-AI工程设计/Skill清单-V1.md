# Skill 清单 — V1.1

> **项目级规则文件**。新增/修改 Skill 前必须查阅本文件。
> AI 编码助手在涉及工作流相关开发时，必须按本文件的约定执行。

---

## 一、Skill 体系总则

### 1.1 核心原则

| 原则 | 说明 |
|---|---|
| 静态方法 | 所有 Skill 必须实现为 `public static class` + 静态方法，禁止使用实例方法 |
| 反射驱动 | 引擎通过反射调用静态方法，反射自动分析方法参数作为输入端口 |
| 登记即用 | 数据库 `wf_skill_reflection` 表登记 classPath + methodName，无需 DI 注册 |
| 唯一约束 | classPath + methodName 在数据库中唯一，避免重复注册 |
| 标准输出统一包装 | 引擎的 `SkillExecutor` 统一包装 `{ success, error, result }`，Skill 方法不关心 |
| 参数绑定模式 | 每个输入参数声明绑定模式（连线/常量/字典），画布按模式渲染 |
| 分类字典维护 | Skill 分类由数据字典维护，不在代码特性中写死 |

### 1.2 废弃清单（彻底抛弃）

| 废弃项 | 原因 |
|---|---|
| `SkillBase` 抽象类 | 静态方法不需要继承 |
| `ISkillNode` 接口 | 静态方法不需要实现接口 |
| `InputDecls` / `OutputDecls` override 属性 | 端口从方法参数反射自动分析 |
| `StandardOutputDecls` 静态属性 | 标准输出由 `SkillExecutor` 统一包装 |
| `ReflectionSkillLoader.Create(typeName)` 实例化逻辑 | 静态方法不需要创建实例 |
| `Program.cs` 中的 `ISkillNode` DI 注册 | 静态方法不需要 DI 注册 |
| `SkillRegistry` 的 `IEnumerable<ISkillNode>` 构造注入 | 不再注入实例 |
| `[Skill(Category)]` 特性字段 | 分类由字典维护 |
| `LlmExtractSkill` | 臆想的 AI 提取 Skill，信息提取走独立流程，不依赖 AI |
| `SideEffect` / `OutputStrict` | 引擎内部概念，不暴露给管理页面 |
| `document_extract` | 文档提取走独立子系统（DocExtractionRuleService），不经过 Skill 体系 |
| `get_field` / `get_table` | 已从 Skill 体系移除，改为前端特殊节点（数据源节点） |

### 1.3 Skill 不做什么

- **不做 AI 提取**：文档信息提取走独立的文档提取流程，结果保存到数据库，不依赖 AI 临时提取
- **不做报告生成**：报告编写按特定规则和流程组织，不是 AI 生成
- **不做状态管理**：Skill 是无状态的业务函数，不维护会话状态
- **不做数据源查询**：`get_field` / `get_table` 不再作为 Skill 存在，改为前端特殊节点

---

## 二、特性体系

### 2.1 `[Skill]` 类级特性（必填）

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class SkillAttribute : Attribute
{
    public string Code { get; set; }                      // Skill 编码，如 "compare"
    public string Name { get; set; }                       // 中文名，如 "值比较"
    public string ReturnType { get; set; } = "json";      // result 类型：string/number/date/boolean/json
    public string Description { get; set; } = "";          // 作用说明
}
```

### 2.2 `[SkillParam]` 参数级特性（可选）

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public class SkillParamAttribute : Attribute
{
    public string Description { get; set; } = "";                          // 参数中文描述
    public SkillParamBindMode BindMode { get; set; }                      // 绑定模式（默认 LinkOrConstant）
    public string? EnumSource { get; set; }                               // 字典编码（BindMode=Enum 时必填）
}

public enum SkillParamBindMode
{
    Link = 0,              // 仅连线：参数值必须来自上游节点输出
    LinkOrConstant = 1,   // 连线或常量：画布上可切换为手动输入（默认）
    Enum = 2              // 字典选择：从 Sys_DictionaryList 按 EnumSource 加载下拉选项
}
```

**使用示例**：

```csharp
// 可连线或手动输入的参数（默认模式）
[SkillParam(Description = "比较值 A（数值/日期/字符串）")]
object? value_a = null,

// 等价于显式声明 BindMode
[SkillParam(Description = "比较值 B", BindMode = SkillParamBindMode.LinkOrConstant)]
object? value_b = null,

// 字典选择参数（不可连线，从字典加载下拉选项）
[SkillParam(Description = "运算符", BindMode = SkillParamBindMode.Enum, EnumSource = "compare_operator")]
string? @operator = null,
```

### 2.3 `[FromService]` 参数级特性（标记依赖注入参数）

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public class FromServiceAttribute : Attribute { }
```

> **注意**：`[FromService]` 参数必须有默认值 `= null!`（C# 可选参数规则要求可选参数后不能有必填参数）。

---

## 三、参数绑定模式

### 3.1 三种绑定模式

| 模式 | 枚举值 | 画布渲染 | 适用场景 | 示例 |
|---|---|---|---|---|
| **仅连线** | `Link` | 输入端口，仅可连线 | 必须从上游获取值 | 文件路径（从上传节点连线） |
| **连线或常量** | `LinkOrConstant` | 输入端口 + 切换按钮 | 既可连线又可手动输入 | 比较值（可从上游取或手动输入） |
| **字典选择** | `Enum` | 下拉选择器，不可连线 | 枚举值选择 | 运算符（> >= < <= == !=） |

### 3.2 字典来源

`BindMode=Enum` 时，`EnumSource` 指定字典编码（`Sys_Dictionary.DicNo`），前端从 Vol 字典接口加载选项。

**已注册的 Skill 字典**：

| 字典编码 | 字典名称 | 项数 | 说明 |
|---|---|---|---|
| `compare_operator` | 比较运算符 | 6 | compare Skill 的 operator 参数 |

字典统一挂载在 `cert_dict`（Dic_ID=107）分类下，遵循 §7.3 字典管理规范。

### 3.3 画布渲染示意图

```
┌─────────────────────────────────┐
│ compare                          │
├─────────────────────────────────┤
│ ● value_a  [🔗 连线 | 📝 常量]   │ ← LinkOrConstant：可切换
│ ● value_b  [🔗 连线 | 📝 常量]   │ ← LinkOrConstant：可切换
│   operator [ ▼ ==             ]  │ ← Enum：下拉选择
├─────────────────────────────────┤
│ ● result                        │
└─────────────────────────────────┘
```

---

## 四、代码编写规范

### 4.1 类结构

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 值比较：接收两个值 + 运算符，执行确定性比较。纯函数，无副作用。
    /// </summary>
    [Skill(
        Code = "compare",
        Name = "值比较",
        ReturnType = "boolean",
        Description = "确定性比较：支持数值比较和日期比较"
    )]
    public static class CompareSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "比较值 A", BindMode = SkillParamBindMode.LinkOrConstant)]
            object? value_a = null,

            [SkillParam(Description = "比较值 B", BindMode = SkillParamBindMode.LinkOrConstant)]
            object? value_b = null,

            [SkillParam(Description = "运算符", BindMode = SkillParamBindMode.Enum, EnumSource = "compare_operator")]
            string? @operator = null,

            CancellationToken ct = default
        )
        {
            // 业务逻辑...
            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object> { ... }));
        }
    }
}
```

### 4.2 编写规则

| 规则 | 说明 |
|---|---|
| 类必须 `public static class` | 静态类，类名以 `Skill` 结尾 |
| 类必须有 `[Skill]` 特性 | 声明 Code / Name / ReturnType / Description |
| 方法名固定 `ExecuteAsync` | 反射查找此方法 |
| 方法返回 `Task<SkillResult>` | 异步，返回 Ok 或 Fail |
| 业务参数在前 | 直接写 C# 类型 + 参数名 |
| 依赖参数用 `[FromService]` | 标记后反射跳过，运行时从 DI 获取，必须有 `= null!` 默认值 |
| `CancellationToken ct = default` 约定最后 | 框架参数，反射跳过 |
| 参数名用 snake_case | 与数据库字段风格一致，如 `value_a` |
| `[SkillParam]` 声明描述 + 绑定模式 | 每个业务参数都应标注 |
| 方法内不需要 try-catch | 异常由 `SkillExecutor` 统一包装 |
| 不需要从 `context.Inputs` 字典取值 | 参数已由反射绑定，直接使用 |

### 4.3 类型映射表

| C# 类型 | 端口类型 | 说明 |
|---|---|---|
| `string` / `string?` | `string` | 不区分可空 |
| `int` / `long` / `double` / `decimal` 及可空版本 | `number` | 不区分可空 |
| `bool` / `bool?` | `boolean` | 不区分可空 |
| `DateTime` / `DateTimeOffset` / `DateOnly` 及可空版本 | `date` | 不区分可空 |
| 其他所有引用类型（含 `object?`） | `json` | 复杂结构一律 json |

### 4.4 必填判定规则

| 条件 | Required | 说明 |
|---|---|---|
| 有默认值（`= xxx`） | `false` | 选填 |
| 无默认值 | `true` | 必填 |

### 4.5 标准输出（引擎统一包装）

```json
{
  "success": true/false,
  "error": "",
  "result": { ... }
}
```

- `success` / `error` 固定，不需要声明
- `result` 的类型由 `[Skill(ReturnType)]` 声明
- `SkillResult.Ok(outputs)` 中的 `outputs` 字典内容放入 `result`
- `SkillExecutor` 负责包装，Skill 方法只返回 Ok 或 Fail

---

## 五、反射执行流程

```
wf_skill_reflection 表
  ├── class_path: "YZH.Core.Skills.CompareSkill"
  └── method_name: "ExecuteAsync"
         │
         ▼
SkillExecutor.ExecuteAsync(skillCode, context, sp, ct)
         │
         ├── 1. 从数据库查 classPath + methodName
         ├── 2. Type.GetType(classPath) → 获取静态类
         │      └── [Skill] 特性 → Code/Name/ReturnType/Description
         ├── 3. type.GetMethod(methodName) → 获取方法
         │      └── GetParameters() → 遍历参数
         │           ├── [FromService] → 跳过，运行时从 DI 获取
         │           ├── CancellationToken → 跳过
         │           └── 其余 → 业务参数（name/type/required/default/description/bindMode/enumSource）
         ├── 4. 必填校验
         ├── 5. 参数绑定（从 context.Inputs 取值 → 类型转换）
         ├── 6. method.Invoke(null, args) → 调用静态方法
         ├── 7. 包装标准输出 { success, error, result }
         └── 8. 返回
```

### 反射验证接口

`POST /api/skill/analyze` — 填入 classPath + methodName，返回反射分析的元数据（含端口信息）。管理页面的"验证反射"按钮调用此接口，人工确认后方可保存。

### 唯一性校验

保存 Skill 时，后端校验 `classPath + methodName` 在 `wf_skill_reflection` 表中唯一（排除自身记录），避免重复注册。

---

## 六、当前 Skill 清单

### 6.1 已建立的 Skill（2 个）

| # | SkillCode | 中文名 | 分类（字典） | ReturnType | C# 实现类 | 依赖 | 说明 |
|---|---|---|---|---|---|---|---|
| 1 | `compare` | 值比较 | data_process | boolean | `YZH.Core.Skills.CompareSkill` | 无 | 数值/日期/字符串比较，纯函数 |
| 2 | `assemble` | 文本拼接 | data_process | string | `YZH.Core.Skills.AssembleSkill` | 无 | 前后两段文本按连接符拼接，纯函数 |

### 6.2 已移除的 Skill

| SkillCode | 原名称 | 移除原因 |
|---|---|---|
| `get_field` | 获取字段值 | 数据源节点，行为与功能性节点不一致，改为前端硬编码特殊节点 |
| `get_table` | 获取表格数据 | 同上 |
| `document_extract` | 文档内容提取 | 文档提取走独立子系统（DocExtractionRuleService），有自己的 Service/Controller/页面，不经过 Skill 体系 |

### 6.3 各 Skill 输入输出端口明细

#### 6.3.1 compare — 值比较

**作用**：接收两个字符串值 + 运算符，执行确定性比较。函数内部自动判断类型（数值/日期/字符串），日期格式由后台统一解析。

| 输入端口 | 类型 | 必填 | 默认值 | 绑定模式 | 字典来源 | 说明 |
|---|---|---|---|---|---|---|
| `value_a` | string | 否 | null | LinkOrConstant | — | 比较值 A（数值/日期/字符串） |
| `value_b` | string | 否 | null | LinkOrConstant | — | 比较值 B（数值/日期/字符串） |
| `operator` | string | 否 | null | Enum | compare_operator | 运算符：> >= < <= == != |

**输出 result**（boolean）：`compare_result`（true/false），日期比较额外输出 `diff_days`。

**compare_operator 字典**：

| DicValue | DicName |
|---|---|
| `>` | 大于 |
| `>=` | 大于等于 |
| `<` | 小于 |
| `<=` | 小于等于 |
| `==` | 等于 |
| `!=` | 不等于 |

#### 6.3.2 assemble — 文本拼接

**作用**：将前半部分文本和后半部分文本按连接符拼接为一个字符串。纯函数，无副作用。

| 输入端口 | 类型 | 必填 | 默认值 | 绑定模式 | 说明 |
|---|---|---|---|---|---|
| `prefix_text` | string | 否 | null | LinkOrConstant | 前半部分文本（合并前） |
| `suffix_text` | string | 否 | null | LinkOrConstant | 后半部分文本（合并后） |
| `joiner` | string | 否 | null | LinkOrConstant | 连接符（空=直接拼接） |

**输出 result**（string）：`assembled_text`。

#### 6.3.3 document_extract — 已移除

> **已移除**（2026-08-19）：文档内容提取走独立子系统（`DocExtractionRuleService`），有自己的 Service/Controller/页面，不经过 Skill 体系。C# 代码文件保留但不注册。

---

## 七、管理页面字段

### 7.1 可编辑字段

| 字段 | 可编辑 | 说明 |
|---|---|---|
| Skill 编码 | 是（新建时） | 唯一标识，如 `compare` |
| Skill 名称 | 是 | 中文名，如「值比较」，反射验证后可用反射值覆盖 |
| 说明 | 是 | Skill 功能说明，反射验证后可用反射值覆盖 |
| 实现类全名 | 是 | 如 `YZH.Core.Skills.CompareSkill`，必填 |
| 方法名 | 是 | 如 `ExecuteAsync`，必填，默认值 `ExecuteAsync` |
| 分类 | 是 | 从字典下拉选择 |
| 启用 | 是 | 开关 |

### 7.2 验证流程

| 步骤 | 操作 | 说明 |
|---|---|---|
| 1. 填写 | 输入 classPath + methodName | 管理员手动填写 |
| 2. 验证 | 点击「验证反射」按钮 | 调用 `POST /api/skill/analyze`，反射提取端口信息 |
| 3. 确认 | 人工查看只读端口列表 | 核实参数名/类型/必填/绑定模式/字典来源 |
| 4. 保存 | 点击「保存」 | 后端再次验证 + 唯一性校验 + 写入数据库 |

> **注意**：打开编辑弹窗时**不自动验证**，必须手动点击验证按钮。保存时检查是否已验证，未验证则拦截。

### 7.3 反射自动填充的只读字段

| 只读字段 | 来源 |
|---|---|
| Skill 名称 | `[Skill(Name)]` |
| 说明 | `[Skill(Description)]` |
| result 类型 | `[Skill(ReturnType)]` |
| 输入端口列表 | 方法参数反射（参数名/类型/必填/默认值/绑定模式/字典来源/描述） |
| 输出端口 | 标准输出（success/error/result）固定 |

---

## 八、数据库表结构

### 8.1 wf_skill（主表）

| 列 | 说明 | 备注 |
|---|---|---|
| id | 主键 | |
| code | GUID | |
| skill_code | Skill 编码 | 唯一，如 `compare` |
| skill_name | 中文名 | 反射同步 |
| category | 分类编码 | 来自字典 |
| description | 说明 | 反射同步 |
| is_active | 启用 | |
| enable | 逻辑删除标记 | |
| status | 状态 | |
| sort_order | 排序 | |
| create_date / creator / modify_date / modifier | 审计字段 | |

### 8.2 wf_skill_reflection（反射配置表）

| 列 | 说明 |
|---|---|
| id | 主键 |
| code | GUID |
| skill_code | Skill 编码 |
| class_path | 实现类全名，如 `YZH.Core.Skills.CompareSkill` |
| method_name | 方法名，如 `ExecuteAsync` |
| enable | 启用 |
| create_date / creator | 审计字段 |

> **唯一索引**：`uk_class_method (class_path, method_name)` — 防止重复注册。

### 8.3 wf_skill_input / wf_skill_output（只读镜像表）

这两个表由反射自动同步，不再手动维护。保存 Skill 时后端反射读取方法参数，自动写入/更新镜像数据。

---

## 九、典型工作流链路

```
[文档提取子系统] 提取结果存入数据库 → [特殊节点] get_field / get_table 查询 → compare 比较 → assemble 拼接 → 输出报告
```

- 文档提取走独立子系统（`DocExtractionRuleService`），不经过 Skill 体系
- 提取结果按规则保存到数据库
- `get_field` / `get_table` 为前端特殊节点（数据源节点），不在此清单管理
- 报告编写按特定流程组织，调 `compare` 做比较判断，`assemble` 拼接文本

---

## 十、文件维护约定

### 10.1 本文件路径

`docs/60-AI工程设计/Skill清单-V1.md`

### 10.2 何时更新

| 场景 | 操作 |
|---|---|
| 新增 Skill | ① 写 C# 静态类 ② 数据库登记 classPath + methodName ③ 更新本文件第六节清单 ④ 更新第六节端口明细 |
| 修改 Skill 参数 | ① 改 C# 方法签名 ② 更新本文件端口明细 |
| 删除 Skill | ① 删除 C# 文件 ② 删除数据库记录 ③ 更新本文件清单 |
| 重命名 Skill | ① 改 `[Skill(Code)]` ② 改数据库 `skill_code` ③ 更新本文件 |
| 新增参数绑定模式字典 | ① 创建 `Sys_Dictionary` + `Sys_DictionaryList` ② 更新本文件第三节 |

### 10.3 文件纳入 AGENTS.md 快速指针

在 `AGENTS.md` 的快速指针中新增一行：
```
- **Skill 清单**：`docs/60-AI工程设计/Skill清单-V1.md` — 全部 Skill 的编码/输入输出/绑定模式/实现类/编写规范
```

---

## 十一、待清理的文件/代码

| 文件/代码 | 操作 | 说明 |
|---|---|---|
| `SkillBase.cs` | 保留但标记 `[Obsolete]` | 过渡期保留，后续删除 |
| `ISkillNode.cs` | 保留但标记 `[Obsolete]` | 过渡期保留 |
| `GetFieldSkill.cs` | 保留代码但从数据库移除注册 | 逻辑保留供前端特殊节点调用 |
| `GetTableSkill.cs` | 保留代码但从数据库移除注册 | 同上 |

---

> 本文件为 Skill 体系的权威约定。代码实现、数据库结构、管理页面均以本文件为准。
> 如有变更，先更新本文件，再改代码。
