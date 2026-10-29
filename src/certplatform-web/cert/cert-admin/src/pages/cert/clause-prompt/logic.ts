/**
 * ClausePromptLogic — 条款属性通用提示词维护（PromptType = clause_attr）
 *
 * 后端：`PromptTemplateController` 的工作台端点（`workbench/*`）
 * 表：`wf_prompt_template` 中 `PromptType = 'clause_attr'` 的行
 *
 * ★ 业务定位（P4）：
 *   条款属性 AI 预生成的提示词需要**持续调优**——模型版本升级、业务口径变化、新增标准…
 *   每次都靠人手工去「条款属性配置」页一条一条改不现实（273 条 × N 属性）。
 *   本页提供「标准 → 提示词正文」的**集中维护入口**：改一次提示词，下次全量生成时用新版。
 *
 * ★ 三层回退（与文档语义规则同构）：标准级（StandardCode = 该标准 GUID）→ 平台级（StandardCode 空）。
 *   用户先从标准下拉选一个 → 显示该标准下的提示词（没有就显示平台级兜底）→ 编辑保存时自动
 *   按 (PromptType, StandardCode) 决定是更新已有行还是新增一行。
 *
 * ★ ⛔ 本章不管：
 *   - doc_group / doc_content / doc_essential 等其他类型的提示词（那是 33 号语义链的事）
 *   - 单条条款的 Artifacts / EvidenceChain 行内提示词（那是 clause-attr 页的行内编辑）
 */
import { SingleTableCore } from '@yzh-core'
import type { YzhTableColumn, YzhFormField } from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { confirmOrFalse } from '@yzh-core'
import {
  activateClausePrompt,
  deleteClausePrompt,
  generateClausePrompt,
  getPromptStandards,
  listClausePrompts,
  saveClausePrompt,
  type ClausePromptItem,
  type ClausePromptStandard,
} from '@share/api/cert/clause-prompt'

/** AI 生成占位符契约（提示词可引用这些由后端渲染注入） */
export const CLAUSE_PROMPT_PLACEHOLDERS = [
  { token: '{{standard_name}}', desc: '标准名 + 可读短码（如「ISO 9001（iso9001）」）' },
  { token: '{{clause_number}}', desc: '条款号（如 7.1.5.2）' },
  { token: '{{clause_title}}', desc: '条款标题' },
  { token: '{{clause_content}}', desc: '条款正文全文' },
]

/** 提示词正文模板（新增时的默认骨架 — 让用户知道该写什么） */
const DEFAULT_TEMPLATE = `# {{standard_name}} · 条款属性预生成提示词

## 任务
你是一个认证审核专家。输入给出 {{standard_name}} 的某一条条款（号 + 标题 + 正文）。
请输出该条款的 8 个属性值，以 JSON 对象返回（key = 属性码，value = 属性值）。

## 条款信息
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：{{clause_content}}

## 输出格式（JSON）
{
  "ClauseIntent": "本条款的意图（一句话）",
  "Obligation": "应",
  "ConstraintStrength": "强制",
  "Artifacts": "企业应提供的资料（提示词正文——要求输出结构化 JSON，只产「读到什么」不产结论）",
  "EvidenceChain": "审核员取证步骤（提示词正文——证据链剧本，不提前给企业）",
  "Applicability": "适用时",
  "Condition": "适用条件",
  "Process": ["涉及过程1", "涉及过程2"],
  "Severity": "高"
}

## 三条铁律
① Artifacts / EvidenceChain 产出的是「提示词正文」（将来跑证据时才用它），不是结论
② 只产「读到什么」不产结论；找不到就交空（null），禁推测
③ Process 是数组（可多选），其余是字符串
`

export class ClausePromptLogic extends SingleTableCore<ClausePromptItem> {
  controllerName = 'Admin/Workflow/PromptTemplate'

  /** 提示词列表（全量：所有标准 + 平台级） */
  promptItems = ref<ClausePromptItem[]>([])

  /** 标准下拉 */
  standardOptions = ref<ClausePromptStandard[]>([])

  /** 当前选中的标准（'' 平台级 / GUID 标准级） */
  selectedStandard = ref('')

  /** 编辑弹窗可见 */
  dialogVisible = ref(false)

  /** 编辑中的行（null = 新增） */
  editingRow = ref<ClausePromptItem | null>(null)

  /** PromptCode 是否可编辑（新增时可改；编辑时禁改 —— 是唯一键） */
  get isCodeEditable(): boolean {
    return this.editingRow.value == null
  }

  /** 提交中 */
  saving = ref(false)

  /** AI 生成中 */
  generating = ref(false)

  /** AI 生成额外要求 */
  genRequirement = ref('')

  /** 加载提示词列表 */
  async loadPrompts(): Promise<void> {
    try {
      this.promptItems.value = await listClausePrompts()
    } catch (e) {
      this.promptItems.value = []
      ElMessage.error('提示词列表加载失败')
    }
  }

  /** 加载标准下拉 */
  async loadStandards(): Promise<void> {
    try {
      this.standardOptions.value = await getPromptStandards()
    } catch {
      this.standardOptions.value = []
    }
  }

  /** 当前选中的标准下的提示词 */
  get filteredPrompts(): ClausePromptItem[] {
    return this.promptItems.value.filter((p) => {
      if (this.selectedStandard.value === '') return !p.standardCode
      return p.standardCode === this.selectedStandard.value
    })
  }

  /** 表格列 */
  get columns(): YzhTableColumn<ClausePromptItem>[] {
    return [
      { prop: 'promptCode', label: '提示词编码', width: 220, showOverflowTooltip: true },
      { prop: 'promptName', label: '名称', minWidth: 200, showOverflowTooltip: true },
      {
        label: '适用标准',
        width: 160,
        slotKey: 'standard',
        slot: true,
      },
      { prop: 'description', label: '说明', minWidth: 160, showOverflowTooltip: true },
      { label: '生效', width: 80, slotKey: 'active', slot: true },
      { label: '操作', width: 200, slotKey: 'actions', slot: true, fixed: 'right' },
    ]
  }

  /** 显示名：展示标准可读短码（而不是 GUID） */
  standardDisplay(code: string | null): string {
    if (!code) return '平台级（全部标准）'
    const s = this.standardOptions.value.find((x) => x.code === code)
    return s?.display || code
  }

  /** 打开新增弹窗 */
  openAdd(): void {
    this.editingRow.value = null
    this.genRequirement.value = ''
    this.dialogVisible.value = true
  }

  /** 打开编辑弹窗 */
  openEdit(row: ClausePromptItem): void {
    this.editingRow.value = row
    this.genRequirement.value = ''
    this.dialogVisible.value = true
  }

  /** 表单字段（新增 / 编辑共用；PromptCode 编辑时禁用） */
  get formFields(): YzhFormField[] {
    const isEdit = this.editingRow.value != null
    return [
      {
        prop: 'promptCode',
        label: '提示词编码',
        required: true,
        placeholder: '如 clause_attr_platform / clause_attr_iso9001',
        disabled: isEdit,
      },
      {
        prop: 'promptName',
        label: '名称',
        required: true,
        placeholder: '如「条款属性预生成提示词（平台级）」',
      },
      {
        prop: 'standardCode',
        label: '适用标准',
        type: 'select',
        placeholder: '留空 = 平台级默认（对所有标准生效）',
        options: [
          { label: '平台级（全部标准）', value: '' },
          ...this.standardOptions.value.map((s) => ({ label: s.display, value: s.code })),
        ],
      },
      {
        prop: 'template',
        label: '提示词正文',
        type: 'textarea',
        required: true,
        placeholder: '提示词正文（支持 {{standard_name}} 等占位符）',
        fieldProps: { rows: 18 },
      },
      {
        prop: 'description',
        label: '说明',
        type: 'textarea',
        placeholder: '可选：这条提示词适用于什么场景、有什么限制',
        fieldProps: { rows: 3 },
      },
    ]
  }

  /** 表单默认值（getter 在新增 / 编辑时被调用） */
  get formDefaults(): Record<string, any> {
    const row = this.editingRow.value
    if (row) {
      return {
        promptCode: row.promptCode,
        promptName: row.promptName,
        standardCode: row.standardCode || '',
        template: row.template || '',
        description: row.description || '',
      }
    }
    // 新增：按当前选中的标准预填
    const stdCode = this.selectedStandard.value
    const std = this.standardOptions.value.find((s) => s.code === stdCode)
    const suffix = std?.standardCode || 'platform'
    return {
      promptCode: `clause_attr_${suffix}`,
      promptName: std
        ? `条款属性预生成提示词（${std.display}）`
        : '条款属性预生成提示词（平台级）',
      standardCode: stdCode,
      template: DEFAULT_TEMPLATE,
      description: '',
    }
  }

  /** 保存 */
  async savePrompt(values: Record<string, any>): Promise<void> {
    this.saving.value = true
    try {
      await saveClausePrompt({
        promptCode: String(values.promptCode || '').trim(),
        promptName: String(values.promptName || '').trim(),
        promptType: 'clause_attr',
        standardCode: values.standardCode || null,
        template: String(values.template || ''),
        description: values.description || null,
      })
      ElMessage.success('提示词已保存')
      this.dialogVisible.value = false
      await this.loadPrompts()
    } catch (e) {
      ElMessage.error(`保存失败：${(e as Error).message || e}`)
    } finally {
      this.saving.value = false
    }
  }

  /** 删除 */
  async deletePrompt(row: ClausePromptItem): Promise<void> {
    // ★ S06：二次确认禁直接调 ElMessageBox.confirm，走 confirmOrFalse 包装
    const ok = await confirmOrFalse(
      `确定删除提示词「${row.promptName}」？删除后不可恢复。`,
      '删除确认',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await deleteClausePrompt(row.promptCode)
      ElMessage.success('已删除')
      await this.loadPrompts()
    } catch (e) {
      ElMessage.error(`删除失败：${(e as Error).message || e}`)
    }
  }

  /** 激活 */
  async activatePrompt(row: ClausePromptItem): Promise<void> {
    try {
      await activateClausePrompt(row.promptCode)
      ElMessage.success('已设为生效版本')
      await this.loadPrompts()
    } catch (e) {
      ElMessage.error(`激活失败：${(e as Error).message || e}`)
    }
  }

  /** AI 生成草稿 → 写入编辑中的 template 字段 */
  async generateDraft(currentTemplate: string): Promise<void> {
    this.generating.value = true
    try {
      const result = await generateClausePrompt({
        promptType: 'clause_attr',
        standardCode: this.selectedStandard.value || null,
        extraRequirement: this.genRequirement.value,
        currentTemplate: currentTemplate || undefined,
      })
      if (!result.prompt) {
        ElMessage.warning(result.message || 'AI 未返回可用草稿')
        return
      }
      // 通过 setter 回写 template
      this.aiDraft.value = result.prompt
      if (result.warning) {
        ElMessage.warning(result.warning)
      } else {
        ElMessage.success('草稿已生成，请确认后保存')
      }
    } catch (e) {
      ElMessage.error(`AI 生成失败：${(e as Error).message || e}`)
    } finally {
      this.generating.value = false
    }
  }

  /** AI 生成的草稿（暂存，用户确认后手动写入 template） */
  aiDraft = ref('')

  /** 写入草稿到表单 */
  applyDraft(draft: string): void {
    // 通知子组件更新 template 字段
    this.aiDraft.value = draft
  }
}

export default ClausePromptLogic
