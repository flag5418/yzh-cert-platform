/**
 * 提示词工作台 —— 静态配置与纯函数（2026-10-02 重建）
 *
 * ⛔ 本页已放弃 `useSingleTable` / `SingleTableCore` 通用 CRUD 渲染 ——
 *    「文档语义规则（提示词工作台）」是「左树 + 中编辑 + 右语义测试」三栏布局，
 *    不是列表页。Controller 路由名 `PromptTemplate` 仍然**永不改名**（改名 = ApiCode 断链）。
 *
 * 后端：PromptTemplateController（/api/PromptTemplate）+ PromptWorkbenchService
 */

import { PROMPT_TYPE, type PromptTemplateDto } from '@share/api/workflow/prompt-workbench'

// ==================== 类型 ====================

export interface PromptTypeDef {
  /** 后端 prompt_type */
  type: string
  /** 中文名（页签） */
  label: string
  /** 树节点徽章文字，如「分类」→ 节点下显示 `[分类]` */
  badge: string
  /** 一句话说明 */
  desc: string
  /**
   * 该类型可用占位符（★ **不带花括号**，点击插入时才补 `{{ }}`）。
   * 后端渲染是单趟替换，提示词正文里写字面占位符会被提前吃掉 —— 这里刻意只列名字。
   */
  placeholders: string[]
}

export interface StandardScope {
  /** 存库值（GUID）；'' = 平台级 / 全部标准 */
  code: string
  label: string
}

// ==================== 常量 ====================

/**
 * 公共占位符（★ 提示词按标准差异化的入口，2026-10-02 补）
 *
 * 重构前种子提示词里既没有 `standard_name` 也没有 `std_doc_catalog`，
 * 导致 `doc_group_iso9001` 与将来的 `doc_group_iso13485` 送给模型的输入逐字节相同
 * —— 33 号 §0.4「标签集合、作用描述都是『标准 × 文档』的函数」落不了地。
 * 后端 `BuildSemanticContextAsync` 已补注入，此处同步暴露给编辑器。
 */
const STD_PLACEHOLDERS = ['standard_name', 'std_doc_catalog', 'folder_tree']

/**
 * 占位符说明（★ 2026-10-03 补）
 *
 * 实施人员维护提示词时最大的障碍不是「规则写得不清楚」，而是
 * **正文里写着 `{{tag_list}}`，但没人知道它运行时会被替换成什么** ——
 * 于是不敢改、看不懂、只能找开发。本表给出人话说明 + 真实数据源，
 * 编辑器以 tooltip 呈现，把「猜」变成「查」。
 *
 * ⛔ 名称必须与后端 `PromptWorkbenchService` 的 `Ph*` 常量逐字一致；
 *    写错一个字 = 占位符静默不替换（渲染后以字面量残留在最终提示词里）。
 */
export const PLACEHOLDER_DOCS: Record<string, { label: string; desc: string }> = {
  standard_name: {
    label: '标准身份',
    desc: '当前标准的标准名 + 版本年 + 类别 + 描述。平台级（未选标准）时注入「未指定标准（平台级默认规则）」，⛔ 不留空串。数据源：cert_iso_standard。'
  },
  std_doc_catalog: {
    label: '标准文档清单',
    desc: '本标准要求的标准文档清单（按 FileName 去重后、按目录顺序，最多 120 份；固定/混合文档带标记）。★ 这是「按标准差异化」的核心输入。数据源：cert_standard_directory_config → cert_standard_directory_file。'
  },
  folder_tree: {
    label: '资料目录结构',
    desc: '本标准的目录文件夹树，让模型知道企业文档可能落在哪一层。数据源：cert_standard_directory_folder。'
  },
  tag_list: {
    label: '标签清单',
    desc: '该标准启用的标签字典，一行一条：编码 | 名称 | 分组 | 识别特征 | 样例 | 作用要点。模型只能从这里选 tagCode。数据源：cert_tag_dict（按 StandardCodes 裁剪）。'
  },
  code_prefix_map: {
    label: '编号前缀规则',
    desc: 'L0 确定性规则：文件编号前缀 → 标签。命中即定标签，优先于语义判断（零 LLM 成本）。数据源：cert_tag_dict.CodePrefix。'
  },
  output_schema: {
    label: '输出契约',
    desc: '下游模型必须遵守的 JSON Schema 文本（后端常量 OutputSchemaJson，不落库）。'
  },
  tag_constraint: {
    label: '标签硬约束',
    desc: '「只能从字典选、依据不足给 OTHER」的约束文案（后端常量 TagConstraintText，不落库）。'
  },
  file_list: {
    label: '文件清单',
    desc: '本批次企业文件清单，JSON 数组（文件名 + 标题 + 开头片段）。仅「分类提示词」使用。'
  },
  document_content: {
    label: '文件全文',
    desc: '单份企业资料转换后的 Markdown 全文。仅「作用提示词」使用。'
  }
}

/** 取占位符说明；未登记的名字返回兜底文案（不静默留空） */
export function describePlaceholder(name: string): string {
  const d = PLACEHOLDER_DOCS[name]
  return d ? `${d.label} —— ${d.desc}` : `${name}（未登记说明）`
}

export const PROMPT_TYPES: PromptTypeDef[] = [
  {
    type: PROMPT_TYPE.Group,
    label: '分类提示词',
    badge: '分类',
    desc: '输入文件清单，输出各文件的标签集合与处理策略',
    placeholders: ['file_list', ...STD_PLACEHOLDERS, 'tag_list', 'code_prefix_map', 'output_schema', 'tag_constraint']
  },
  {
    type: PROMPT_TYPE.Content,
    label: '作用提示词',
    badge: '作用',
    desc: '输入单文件 Markdown，输出作用、关键要素与结构',
    placeholders: ['document_content', ...STD_PLACEHOLDERS, 'tag_list', 'output_schema', 'tag_constraint']
  }
]

export const TYPE_MAP: Record<string, PromptTypeDef> = Object.fromEntries(
  PROMPT_TYPES.map((t) => [t.type, t])
)

/** 根节点：全部标准（承载平台级默认规则） */
export const ROOT_SCOPE: StandardScope = { code: '', label: '全部标准' }

/** 分组提示词输出子集字段（33 号 §3.4 方案 B，避免无用字段） */
export const GROUP_OUTPUT_FIELDS = 'index / fileName / tags / suggestedPolicy / policyReason / purpose / confidence'

// ==================== 纯函数 ====================

/** 作用域口径统一：null / undefined / 空白 一律视为「平台级」 */
export function normScope(code?: string | null): string {
  return (code || '').trim()
}

/** 该作用域下该类型是否存在提示词（供树节点 `[分类]` `[作用]` 徽章） */
export function hasPrompt(
  rows: PromptTemplateDto[],
  promptType: string,
  scope: string
): boolean {
  const target = normScope(scope)
  return rows.some(
    (r) => r.promptType === promptType && normScope(r.standardCode) === target
  )
}

/**
 * 取「**恰好落在本作用域**」的那条提示词。
 *
 * ⚠️ 不能用 `resolve` 的结果直接当 promptCode —— resolve 会回退到平台级，
 * 拿平台级的 promptCode + 本作用域去保存，会把平台级那行**改挂**到本标准，
 * 导致平台默认规则凭空消失。
 */
export function findScopeRow(
  rows: PromptTemplateDto[],
  promptType: string,
  scope: string
): PromptTemplateDto | null {
  const target = normScope(scope)
  return rows.find((r) => r.promptType === promptType && normScope(r.standardCode) === target) ?? null
}

/** 新作用域的 PromptCode（本作用域没有行时才走到这里） */
export function makePromptCode(promptType: string, scope: string): string {
  const tail = normScope(scope) ? normScope(scope).slice(0, 8) : 'platform'
  // ⚠️ 不用 Date.now()：同一毫秒内连续两次调用会产出**完全相同**的 PromptCode，
  //    而 PromptCode 是唯一业务键（准则 A：按 Code 幂等 upsert）——
  //    撞码的后果是「新建」静默变成「覆盖另一条」，且界面显示保存成功。
  //    随机串消除时间依赖：6 位 base36 ≈ 21.8 亿组合，人工建码不可能撞。
  const rnd = Math.random().toString(36).slice(2, 8).padEnd(6, '0')
  return `${promptType}_${tail}_${rnd}`
}

/** 提示词名称：作用域 + 类型，便于在列表里认出来 */
export function makePromptName(type: string, scopeLabel: string): string {
  const def = TYPE_MAP[type]
  return `${scopeLabel} · ${def ? def.label : type}`
}

/** 校验正文非空 */
export function isBlankTemplate(text?: string | null): boolean {
  return !text || !text.trim()
}

export default PROMPT_TYPES
