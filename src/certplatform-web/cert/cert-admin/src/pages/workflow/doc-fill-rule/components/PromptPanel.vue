<script setup lang="ts">
/**
 * 全文填写规则 —— **文本框 + 自动生成（后端 LLM）+ 保存** 三件套。
 *
 * 【★ 2026-10-07 用户裁决（第二轮 4 项改造之①）】
 *   ① 文本框内容 = 当前挂接提示词的 `UserTemplate`；
 *   ② 未挂接时点「自动生成」⇒ **自动 `addDocFillPrompt` + `setDocTemplatePrompt`**（先有鸡先有蛋就地破环）；
 *   ③ 保存 = 更新该版本；**版本表 / 挂接对话框 / 解绑 / 编辑抽屉全删**，只留文本框。
 *
 * 【三条通路的关系（与「锚点规则」并列，不互斥）】
 *   - 锚点规则（`cert_doc_template_anchor`）：确定性落笔，逐处 `{AnchorCode: value}`
 *   - 全文规则（本组件）：整篇文档的结构、行文口吻与颗粒度
 *   - 引擎先跑锚点，全文提示词只负责「通篇组织」——生成提示词的后端
 *     `BuildGeneratePrompt` 已把这条边界写进提示词正文。
 *
 * 【★ 自动生成 = 后端调 LLM】`POST /DocFillPrompt/generate`：
 *   传「锚点清单 + 文档作用 + 文档角色 + 现有正文」，返回提示词正文（**不落库**）。
 *   `CurrentPrompt` 非空 = 优化模式（保留结构不全量重写）。
 *   生成成功后：未挂接 ⇒ 就地新建+挂接；已挂接 ⇒ 写入文本框，点「保存」才生效。
 *
 * 【★ 非法引用 / 错误只上报，⛔ 不在本卡片里堆大块提示】
 *   页签空间有限（用户第 4 项裁决）⇒ 非法引用经 `invalidRefs` 事件交给父页，
 *   统一显示在「全局规则」Tab 的**角标弹层**里。
 */
import { MagicStick } from '@element-plus/icons-vue'
import {
  addDocFillPrompt,
  generateFillPrompt,
  getDocFillPromptVersions,
  resolveDocFillPrompt,
  setDocTemplatePrompt,
  updateDocFillPrompt,
  type DocFillPromptVersion,
} from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, inject, nextTick, onMounted, ref, watch } from 'vue'
import type { DocFillRuleLogic } from '../logic'
import MaterialArea from './MaterialArea.vue'
import { genPromptCode } from './promptCode'
import { parseSourceSpec, summarizeSourceSpec } from './sourceSpec'

const props = defineProps<{
  /** 当前模板 Code（未挂接时新建提示词后挂到它身上） */
  templateCode: string
  /** 已挂接的提示词编码；空串 = 未挂接 */
  promptCode: string
  /** 模板所属机构（提示词「机构更具体优先」的选取依据） */
  orgCode: string
  /** 模板名（新建提示词的默认名） */
  templateName: string
  /** 文档作用（生成上下文；来自契约，父页读 `DocContractDetail.DocPurpose`） */
  docPurpose?: string
  /** 文档角色编码（生成上下文，可空） */
  docRole?: string
}>()

const emit = defineEmits<{
  /** 挂接成功 ⇒ 父页刷新左树徽标（`FillPromptCode` 已变） */
  (e: 'bound', promptCode: string): void
  /** 非法引用的锚点名 ⇒ 父页放进「全局规则」角标弹层（⛔ 本卡片不堆错误块） */
  (e: 'invalidRefs', refs: string[]): void
}>()

const logic = inject<DocFillRuleLogic>('logic')

/** ★ 锚点清单 = 素材区、已引用清单、生成上下文的**唯一来源**（读共享仓库，见 logic.ensureAnchors） */
const anchors = computed<any[]>(() => logic?.anchorRows ?? [])

/* ============ 状态 ============ */
/** 文本框正文（= 生效版本的 `UserTemplate`） */
const text = ref('')
/** 生效版本解析结果（`resolve` 返回：`Found` / `Picked` / `CandidateCount`） */
const resolved = ref<any>(null)
/**
 * 生效版本的**整行**（`versions` 端点取回）—— 保存时整行提交 update。
 *
 * ⛔ 不能只发 `{Code, UserTemplate}`：`updateFields` 是全部 `BcFlag` 列，
 *    缺的字段会被写成 CLR 默认值（把 `PromptName` / `Sort` 等清掉）。
 */
const pickedRow = ref<DocFillPromptVersion | null>(null)
/**
 * 本地挂接码：新建+挂接成功到父页 `reloadTree` 把 `promptCode` 刷回来之间，
 * 有一小段窗口 —— 没有它，`hasPrompt` 会在窗口内仍为 false，
 * 用户连点两次「保存」会建出两个提示词。
 */
const localCode = ref('')

const loading = ref(false)
const generating = ref(false)
const saving = ref(false)

const effectiveCode = computed(() => props.promptCode || localCode.value)
const hasPrompt = computed(() => !!effectiveCode.value)

const fillTokenSample = '{{__FILL__.锚点}}'

/** 非法引用（`{{__FILL__.X}}` 里 X 不在本模板锚点中）—— 只上报，不在卡片里堆错误块 */
const invalidPromptRefs = computed(() => {
  if (!logic) return []
  return logic.validatePromptAnchors(text.value, anchors.value)
})
watch(
  invalidPromptRefs,
  (refs) => emit('invalidRefs', refs),
  { immediate: true },
)

/**
 * 生成上下文用的锚点清单：**全部存活锚点**（不只是 ai 来源）——
 * 后端要把「哪些位置会被自动填写」整体交代给模型，只传 ai 锚点会让它误以为
 * 其余格子不存在，从而在提示词里越界规定单个锚点的取值。
 */
const genAnchors = computed(() =>
  anchors.value
    .filter((a) => !a.IsOrphan)
    .map((a) => ({
      AnchorRef: String(a.AnchorRef ?? ''),
      ValueType: String(a.ValueType || 'text'),
      Source:
        summarizeSourceSpec(parseSourceSpec(a.SourceSpec).model) || '未配置',
    }))
    .filter((a) => a.AnchorRef),
)

/* ============ 加载 ============ */
/**
 * 解析生效版本 + 取整行（并行）。
 *
 * ⚠️ `resolve` 是**选取权威**（机构优先 → 默认优先 → 版本新优先）；
 *    `versions` 只负责把选中那行的**完整列**拿回来供 update 整行提交 ——
 *    两者可见范围在后端已对齐（`versions` 注释），⛔ 不要在前端自己重排选取。
 */
async function loadResolved(code = effectiveCode.value) {
  if (!code) {
    resolved.value = null
    pickedRow.value = null
    text.value = ''
    return
  }
  loading.value = true
  try {
    const [r, v] = await Promise.all([
      resolveDocFillPrompt(code, props.orgCode),
      getDocFillPromptVersions(code, props.orgCode),
    ])
    resolved.value = unwrapOk(r, '解析生效版本失败') ?? null
    const items = (unwrapOk(v, '加载提示词版本失败')?.Items ??
      []) as DocFillPromptVersion[]
    const pickedCode = resolved.value?.Found ? resolved.value.Picked?.Code : ''
    pickedRow.value =
      items.find((i) => i.Code === pickedCode && !i.IsDeleted) ?? null
    // 正文以整行为准；整行缺 `UserTemplate` 时回落 resolve 的 Picked
    text.value =
      pickedRow.value?.UserTemplate ??
      resolved.value?.Picked?.UserTemplate ??
      ''
  } catch {
    // 解析失败 ⛔ 不打挂卡片：文本框留空，用户仍可生成/保存
    resolved.value = null
    pickedRow.value = null
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  // 锚点只读共享仓库；这里仅兜底（幂等，父页拉过就不发请求）
  void logic?.ensureAnchors()
  void loadResolved()
})

// 切文件 ⇒ 清本地挂接码并重载
watch(
  () => props.templateCode,
  () => {
    localCode.value = ''
    void loadResolved()
  },
)
// 挂接码变化（父页刷树回来）⇒ 重载
watch(effectiveCode, (code) => void loadResolved(code))

/* ============ 素材插入 ============ */
/** 裸 `<textarea>` 的 DOM 引用（插入素材时读/摆光标） */
const textareaEl = ref<HTMLTextAreaElement | null>(null)

/**
 * 把 `val` 插到 `el` 当前光标处（无光标 ⇒ 追加末尾）。
 *
 * ⛔ 必须**改状态**再在 `nextTick` 摆回光标 —— 直接改 DOM value + 伪造
 * `input` 事件是绕过 Vue 响应式的写法，绑定方式一变就静默失效。
 */
function insertAtCursor(val: string) {
  const el = textareaEl.value
  const current = text.value
  const hasCursor = !!el && typeof el.selectionStart === 'number' && el.isConnected
  const start = hasCursor ? el.selectionStart : current.length
  const end = hasCursor ? el.selectionEnd : current.length
  text.value = current.slice(0, start) + val + current.slice(end)
  if (!hasCursor) return
  nextTick(() => {
    const node = el as HTMLTextAreaElement
    node.focus()
    node.setSelectionRange(start + val.length, start + val.length)
  })
}

/** 已引用清单：从正文提取 `{{token}}` 并标注是否已知 */
const promptRefs = computed(() => {
  const matches = [...new Set(text.value.match(/\{\{[^{}]{1,60}\}\}/g) || [])]
  const known = new Set<string>()
  anchors.value.forEach((a) => known.add(`{{__FILL__.${a.AnchorRef}}}`))
  known.add('{{__NOW__}}')
  known.add('{{__PROFILE__.}}')
  return matches.map((t) => ({
    token: t,
    known: known.has(t) || /\{\{[A-Z0-9_]+\}\}/.test(t),
  }))
})

/* ============ 自动生成 ============ */
/**
 * ★ 自动生成 —— 后端 LLM 按「锚点清单 + 文档作用」产出正文（约 5~15s）。
 *
 * - 未挂接 ⇒ 生成后**就地新建 + 挂接**（用户裁决①，破「先有鸡先有蛋」）；
 * - 已挂接 ⇒ 只写进文本框，点「保存」才落库（生成 ≠ 保存，改坏了还能重来）。
 */
async function onGenerate() {
  if (!props.templateCode) {
    ElMessage.warning('请先在左侧选择一个已上传空白模板的文件')
    return
  }
  if (!props.docPurpose?.trim() && genAnchors.value.length === 0) {
    ElMessage.warning('缺少生成依据：文档作用与锚点清单至少给一项')
    return
  }

  generating.value = true
  try {
    const d = unwrapOk(
      await generateFillPrompt({
        DocPurpose: props.docPurpose?.trim() || undefined,
        DocRole: props.docRole || undefined,
        // 非空 = 优化模式（后端据此切换提示词模式）
        CurrentPrompt: text.value.trim() || undefined,
        Anchors: genAnchors.value,
      }),
      'AI 生成失败',
    )
    text.value = d.Prompt || ''
    if (!text.value.trim()) {
      ElMessage.warning('AI 未返回内容，请重试')
      return
    }
    if (!hasPrompt.value) {
      // ★ 用户裁决①：未挂接时自动生成 = 生成 + 新建 + 挂接 一气呵成
      await persist()
      ElMessage.success('已生成并新建挂接提示词（v1 草稿）')
    } else {
      ElMessage.success(
        d.Optimize ? '已按现有正文优化，点「保存」生效' : '已生成草稿，点「保存」生效',
      )
    }
  } catch (e: any) {
    ElMessage.error(e?.message || 'AI 生成失败')
  } finally {
    generating.value = false
  }
}

/* ============ 保存 ============ */
/**
 * 落库：已挂接 ⇒ 整行 update；未挂接 ⇒ 新建 v1 + 挂接。
 *
 * ⚠️ 新建与挂接是**两个实体、跨控制器，非事务** —— 第二步失败时提示词已存在，
 * 如实告知（`setDocTemplatePrompt` 的失败会抛出，由调用方展示）。
 */
async function persist() {
  if (hasPrompt.value) {
    if (!pickedRow.value) {
      // 生效版本解析失败（如刚被删）⇒ 重试一次；仍失败则如实报错，⛔ 不回落 Id
      await loadResolved()
      if (!pickedRow.value)
        throw new Error('找不到当前生效版本，无法保存')
    }
    unwrapOk(
      await updateDocFillPrompt({
        ...pickedRow.value,
        UserTemplate: text.value,
      }),
      '保存失败',
    )
    pickedRow.value = { ...pickedRow.value, UserTemplate: text.value }
    return
  }

  const code = genPromptCode()
  const name = props.templateName ? `${props.templateName} 全文填写` : '全文填写规则'
  unwrapOk(
    await addDocFillPrompt({
      PromptCode: code,
      PromptName: name,
      UserTemplate: text.value || undefined,
    }),
    '新建提示词失败',
  )
  unwrapOk(await setDocTemplatePrompt(props.templateCode, code), '挂接失败')
  localCode.value = code
  emit('bound', code)
  // 把新行的整行取回来（后续保存走 update）
  await loadResolved(code)
}

async function onSave() {
  if (!text.value.trim() && !hasPrompt.value) {
    ElMessage.warning('提示词内容为空 —— 先编写或点「自动生成」')
    return
  }
  saving.value = true
  try {
    await persist()
    ElMessage.success('全文填写规则已保存')
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="prompt-panel" v-loading="loading">
    <!-- 状态行：挂接态 + 生效版本（⛔ 不是大块提示，只有一行徽标） -->
    <div class="pp-hd">
      <YzhStatusBadge
        v-if="hasPrompt"
        type="success"
        :text="`已挂接 ${effectiveCode}`"
      />
      <YzhStatusBadge v-else type="info" text="未挂接 · 保存时自动新建" />
      <span v-if="resolved?.Found" class="pp-hd__meta">
        生效 v{{ resolved.Picked.Version }} · {{ resolved.Picked.PromptName }}
        <template v-if="resolved.CandidateCount > 1">
          （共 {{ resolved.CandidateCount }} 个候选版本）
        </template>
      </span>
      <span v-else-if="hasPrompt && resolved && !resolved.Found" class="pp-hd__meta pp-hd__meta--bad">
        生效版本不存在 —— 运行期不走全文规则
      </span>
      <span class="sp"></span>
    </div>

    <!-- 素材区：锚点 chip 光标处插入 -->
    <div class="pp-mats">
      <MaterialArea :anchors="anchors" @select="(v: string) => insertAtCursor(v)" />
    </div>

    <!-- 编辑区 -->
    <div class="pp-ed">
      <div class="hint">
        光标处插入素材，或直接编写；支持 {{ fillTokenSample }} 引用锚点。
        本规则只管通篇组织，单个格子填什么由锚点规则负责。
      </div>
      <textarea
        ref="textareaEl"
        v-model="text"
        placeholder="输入全文填写提示词 —— 或点「自动生成」由 AI 按文档作用与锚点清单产出"
      ></textarea>

      <!-- 已引用清单（紧凑一行；未知 token 标红，明细看角标弹层） -->
      <div class="pp-ref">
        <span v-if="promptRefs.length" class="refhd">已引用：</span>
        <span
          v-for="t in promptRefs"
          :key="t.token"
          class="refchip"
          :class="{ unk: !t.known }"
          :title="t.known ? '可识别' : '⚠️ 未知 token'"
        >
          {{ t.token }}
        </span>
      </div>
    </div>

    <!-- 操作行 -->
    <div class="pp-actions">
      <el-button
        type="default"
        size="small"
        :icon="MagicStick"
        :loading="generating"
        :disabled="!templateCode"
        title="按「锚点清单 + 文档作用」由 AI 生成提示词正文"
        @click="onGenerate"
      >
        自动生成
      </el-button>
      <el-button
        type="primary"
        size="small"
        :loading="saving"
        :disabled="!text.trim() && !hasPrompt"
        @click="onSave"
      >
        保存
      </el-button>
      <span class="sp"></span>
      <span class="pp-actions__meta">{{ text.length }} 字</span>
    </div>
  </div>
</template>

<style scoped>
.prompt-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.sp {
  flex: 1;
}

/* ── 状态行 ── */
.pp-hd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  margin-bottom: var(--yzh-space-2, 8px);
}
.pp-hd__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.pp-hd__meta--bad {
  color: var(--yzh-color-danger, #dc2626);
}

/* ── 素材区 ── */
.pp-mats {
  border: 1px solid var(--yzh-color-border-light, #e4e7ed);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-subtle, #fbfcfe);
  padding: var(--yzh-space-1, 5px) var(--yzh-space-2, 9px);
  flex-shrink: 0;
}

/* ── 编辑区 ── */
.pp-ed {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 7px;
  margin-top: var(--yzh-space-2, 8px);
}

.hint {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 1.6;
  margin: 0;
}

.pp-ed textarea {
  width: 100%;
  flex: 1;
  min-height: 180px;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  resize: vertical;
  padding: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border, #dcdfe6);
  border-radius: var(--yzh-radius-md, 8px);
  outline: none;
  background: var(--yzh-color-bg-container, #fff);
  color: var(--yzh-color-text-primary, #303133);
}
.pp-ed textarea:focus {
  border-color: var(--yzh-color-primary, #1e3a8a);
  box-shadow: 0 0 0 2px var(--yzh-color-primary-light-9, #ecf5ff);
}

.pp-ref {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  align-items: center;
  min-height: 24px;
  padding: var(--yzh-space-1, 4px) 0;
}
.pp-ref .refhd {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
}
.refchip {
  font-size: var(--yzh-font-size-xs, 11px);
  padding: var(--yzh-space-1, 2px) var(--yzh-space-2, 8px);
  border-radius: 12px;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  color: var(--yzh-color-primary, #1e3a8a);
  border: 1px solid var(--yzh-color-primary-light-8, #d9ecff);
}
.refchip.unk {
  background: var(--yzh-color-danger-light-9, #fef0f0);
  color: var(--yzh-color-danger, #dc2626);
  border-color: var(--yzh-color-danger-light-8, #fde2e2);
}

/* ── 操作行 ── */
.pp-actions {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-3, 12px);
  margin-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
  flex-shrink: 0;
}
.pp-actions__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}
</style>
