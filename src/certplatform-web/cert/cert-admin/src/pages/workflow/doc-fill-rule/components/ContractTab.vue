<script setup lang="ts">
/**
 * Tab3 · 文档契约（可编辑）
 *
 * 【⛔ 这里与 `37` 号 §3.4 Tab3 的原始设计不同 —— 是**实测推翻**的结果】
 *   原设计写的是「契约**只读** + 跳转到『文档语义规则』页编辑」，理由是
 *   「分类/作用属契约，不是模板属性；两处改同一份数据 = 必然不一致」。
 *   该理由**仍然成立**，但前提不成立：实查 `cert_standard_doc_contract` **0 行，
 *   且全项目没有任何读写它的端点** —— 跳转过去也没有页面能改。
 *   ⇒ 本页承担契约的读写；后端 `save` 会**同批同步** `cert_standard_directory_file.DocCategory`
 *     （流程分叉的权威列），保证两处永不不一致。
 *
 * 【「这个文档是否不需要编辑」就是这里的 `DocCategory`】
 *   `editable` = 可编辑文档 → 要配填写规则（组 E）
 *   `fixed`    = 固定格式文档（PDF/图片/扫描件）→ **免填**，只配指纹（组 F）
 *   `hybrid`   = 混合（一期**不启用**，`37` 号 Q-10）
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus, RefreshRight, MagicStick, Close, Document } from '@element-plus/icons-vue'
import { unwrapOk, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import {
  saveDocContract,
  type DocContractDetail,
} from '@share/api/workflow/doc-fill-rule'

/**
 * ⚠️ 契约的**读取状态由父页持有**（`detail` 是 prop，不是本组件自己拉）。
 *
 * 理由：父页也要用 `DocCategory`（决定操作条启用/禁用、右栏页签组）和
 * 标准文档的存储路径（决定 `[下载标准文档]` 能不能点）。
 * 两处各拉一份 = 两处各存一份状态 ⇒ 保存后必有一处是旧值。
 */
const props = defineProps<{
  /** 契约详情（父页加载）。`null` = 未选中文档 */
  detail: DocContractDetail | null
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'saved'): void
  (e: 'reload'): void
}>()

const saving = ref(false)

/* ============ 编辑态（由 detail 初始化） ============ */
const form = ref({
  DocCategory: 'editable',
  DocRole: 'required',
  DocName: '',
  DocPurpose: '',
})
const tags = ref<string[]>([])
const infoItems = ref<string[]>([])
const newTag = ref('')
const newItem = ref('')

const DOC_ROLES = [
  { value: 'required', label: '必需提供' },
  { value: 'optional', label: '可选提供' },
  { value: 'reference', label: '参考资料' },
  { value: 'attachment', label: '附件' },
]

const CATEGORY_OPTIONS = [
  {
    value: 'editable',
    label: '可编辑文档',
    desc: '要配填写规则（锚点 + 全文规则）',
  },
  {
    value: 'fixed',
    label: '固定格式（免填）',
    desc: 'PDF / 图片 / 扫描件 —— 不生成内容，只配指纹',
  },
]

/** 分析状态 → 人话 + 颜色 */
const analyzeMeta = computed(() => {
  const s = props.detail?.AnalyzeStatus || 'pending'
  const map: Record<string, { text: string; type: any }> = {
    pending: { text: '尚未分析', type: 'info' },
    manual: { text: '人工录入（未跑过 AI）', type: 'info' },
    analyzing: { text: '分析中', type: 'warning' },
    completed: { text: '已完成', type: 'success' },
    failed: { text: '分析失败', type: 'danger' },
  }
  return map[s] || { text: s, type: 'info' }
})

/** 安全解析 JSON 数组（历史数据可能是空串 / 半截 JSON） */
function parseArray(raw?: string | null): string[] {
  const t = (raw ?? '').trim()
  if (!t) return []
  try {
    const v = JSON.parse(t)
    return Array.isArray(v) ? v.map((x) => String(x)) : []
  } catch {
    return []
  }
}

/** 父页加载完契约 / 切换文档 ⇒ 重置编辑态 */
watch(
  () => props.detail,
  (d) => {
    if (!d) return
    form.value = {
      DocCategory: d.DocCategory || 'editable',
      DocRole: d.DocRole || 'required',
      DocName: d.DocName || d.FileName || '',
      DocPurpose: d.DocPurpose || '',
    }
    tags.value = parseArray(d.TagsJson)
    infoItems.value = parseArray(d.InfoItemsJson)
  },
  { immediate: true },
)

/* ============ 编辑 ============ */
function addTag() {
  const t = newTag.value.trim()
  if (!t) return
  if (!tags.value.includes(t)) tags.value.push(t)
  newTag.value = ''
}
function removeTag(i: number) {
  tags.value.splice(i, 1)
}
function addItem() {
  const t = newItem.value.trim()
  if (!t) return
  if (!infoItems.value.includes(t)) infoItems.value.push(t)
  newItem.value = ''
}
function removeItem(i: number) {
  infoItems.value.splice(i, 1)
}

/* ============ 保存 ============ */
async function onSave() {
  const fileCode = props.detail?.StandardFileCode
  if (!fileCode) return
  saving.value = true
  try {
    unwrapOk(
      await saveDocContract({
        StandardFileCode: fileCode,
        DocName: form.value.DocName,
        DocCategory: form.value.DocCategory,
        DocRole: form.value.DocRole,
        DocPurpose: form.value.DocPurpose,
        // ★ 空数组必须提交 `'[]'`，⛔ **不能提交空串**（2026-10-04 实测缺陷）：
        //   `TagsJson` / `InfoItemsJson` 是 MySQL `json` 列，写空串会在 INSERT 阶段抛
        //   `Invalid JSON text: "The document is empty." at position 0` ⇒ **整条契约保存失败**，
        //   而前端只会显示「新增失败：…」，完全看不出根因是「标签为空」。
        //   `JSON.stringify([])` === `'[]'`，是合法 JSON 空数组。
        TagsJson: JSON.stringify(tags.value),
        InfoItemsJson: JSON.stringify(infoItems.value),
      }),
      '保存文档契约失败',
    )
    ElMessage.success('文档契约已保存（标准目录行的分类已同步）')
    emit('saved')
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="contract-tab" v-loading="loading">
    <template v-if="detail">
      <!-- ★ 「这个文档是否不需要编辑」 -->
      <section class="block">
        <h4 class="block__title">
          是否不需要编辑
          <span class="block__sub">决定右栏用哪套规则；保存后同步到标准目录行</span>
        </h4>
        <el-radio-group v-model="form.DocCategory" class="cat-group">
          <el-radio
            v-for="c in CATEGORY_OPTIONS"
            :key="c.value"
            :value="c.value"
            class="cat-item"
            border
          >
            <div class="cat-item__label">{{ c.label }}</div>
            <div class="cat-item__desc">{{ c.desc }}</div>
          </el-radio>
        </el-radio-group>
        <div class="hint-line">
          <code>hybrid</code>（混合）一期不启用 —— 传了也不会落库。
        </div>
      </section>

      <!-- 语义 -->
      <section class="block">
        <h4 class="block__title">文档语义</h4>
        <el-form label-width="80px" label-position="left" size="small">
          <el-form-item label="文档名称">
            <el-input v-model="form.DocName" />
          </el-form-item>
          <el-form-item label="文档角色">
            <el-select v-model="form.DocRole" style="width: 100%">
              <el-option v-for="r in DOC_ROLES" :key="r.value" :value="r.value" :label="r.label" />
            </el-select>
          </el-form-item>
          <el-form-item label="文档作用">
            <el-input
              v-model="form.DocPurpose"
              type="textarea"
              :rows="3"
              placeholder="这份文档在认证流程里干什么用（人读的四段式描述）"
            />
          </el-form-item>
        </el-form>

        <div class="sub-block">
          <div class="sub-block__title">标签</div>
          <div class="chip-list">
            <span v-for="(t, i) in tags" :key="t" class="chip">
              {{ t }}
              <el-icon class="chip__close" @click="removeTag(i)"><Close /></el-icon>
            </span>
            <span v-if="!tags.length" class="muted">暂无标签</span>
          </div>
          <div class="add-row">
            <el-input
              v-model="newTag"
              size="small"
              placeholder="输入标签后回车（值应来自标签字典）"
              style="width: 260px"
              @keyup.enter="addTag"
            />
            <el-button type="default" size="small" :icon="Plus" @click="addTag">添加</el-button>
          </div>
        </div>

        <div class="sub-block">
          <div class="sub-block__title">包含信息</div>
          <div class="chip-list">
            <span v-for="(t, i) in infoItems" :key="t" class="chip chip--info">
              {{ t }}
              <el-icon class="chip__close" @click="removeItem(i)"><Close /></el-icon>
            </span>
            <span v-if="!infoItems.length" class="muted">暂无条目</span>
          </div>
          <div class="add-row">
            <el-input
              v-model="newItem"
              size="small"
              placeholder="这份文档里包含哪些信息项（如：审核日期 / 审核组长）"
              style="width: 320px"
              @keyup.enter="addItem"
            />
            <el-button type="default" size="small" :icon="Plus" @click="addItem">添加</el-button>
          </div>
        </div>
      </section>

      <!-- 分析元数据（只读） -->
      <section class="block">
        <h4 class="block__title">分析来源<span class="block__sub">只读</span></h4>
        <el-descriptions :column="2" size="small" border>
          <el-descriptions-item label="分析状态">
            <YzhStatusBadge :type="analyzeMeta.type" :text="analyzeMeta.text" />
          </el-descriptions-item>
          <el-descriptions-item label="人工修正">
            <YzhStatusBadge v-if="detail.IsManualCorrected" type="warning" text="已人工修正" />
            <span v-else class="muted">否</span>
          </el-descriptions-item>
          <el-descriptions-item label="标签来源">
            {{ detail.TagsSource || '—' }}
            <span v-if="detail.TagsConfidence != null" class="muted">
              （置信 {{ detail.TagsConfidence }}）
            </span>
          </el-descriptions-item>
          <el-descriptions-item label="作用来源">
            {{ detail.DocPurposeSource || '—' }}
            <span v-if="detail.DocPurposeConfidence != null" class="muted">
              （置信 {{ detail.DocPurposeConfidence }}）
            </span>
          </el-descriptions-item>
          <el-descriptions-item label="模型" :span="2">
            {{ detail.ModelName || '—' }}
            <span v-if="detail.AnalyzeTime" class="muted"> · {{ detail.AnalyzeTime }}</span>
          </el-descriptions-item>
        </el-descriptions>
        <div class="hint-line">
          <el-icon><MagicStick /></el-icon>
          语义分析（分类 / 作用 / 标签）本轮<strong>尚未接通</strong> —— 可先在此手工填写；
          一旦人工保存，后续批量重跑<strong>不会覆盖</strong>你的值。
        </div>
      </section>

      <div class="actions">
        <el-button
          type="default"
          :icon="RefreshRight"
          :loading="loading"
          @click="emit('reload')"
        >
          重新读取
        </el-button>
        <el-button type="primary" :loading="saving" @click="onSave">保存契约</el-button>
      </div>
    </template>

    <YzhEmptyState v-else :icon="Document" title="请先在左侧选择一个文档" />
  </div>
</template>

<style scoped>
.contract-tab {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
}

.block__title {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
}
.block__sub {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-normal, 400);
  color: var(--yzh-color-text-secondary, #606266);
}

.cat-group {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  align-items: stretch;
}
.cat-item {
  height: auto;
  margin: 0;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
}
.cat-item__label {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-medium, 500);
}
.cat-item__desc {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  white-space: normal;
  line-height: var(--yzh-line-height-tight, 1.3);
}

.hint-line {
  margin-top: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  line-height: var(--yzh-line-height-base, 1.6);
}

.sub-block {
  margin-top: var(--yzh-space-3, 12px);
}
.sub-block__title {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  margin-bottom: var(--yzh-space-1, 4px);
}
.chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-1, 4px);
  min-height: 26px;
  align-items: center;
}
/* 可关闭标签片（S08 只允许 YzhStatusBadge 表达「状态」；标签片是数据不是状态） */
.chip {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-muted, #f3f4f6);
  color: var(--yzh-color-text-regular, #606266);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1;
}
.chip--info {
  background: var(--yzh-color-bg-hover, #f1f5f9);
  color: var(--yzh-color-text-muted, #64748b);
}
.chip__close {
  cursor: pointer;
  font-size: var(--yzh-font-size-xs, 12px);
}
.add-row {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-2, 8px);
}
.muted {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-1, 4px);
}
</style>
