<script setup lang="ts">
/**
 * 全局规则 · 「文档属性」(ContractTab)
 *
 * 【★ 2026-10-05 第 28 轮（C1/C3）重构：从「识别与类型」收敛为「文档属性」】
 *   原型 V1 把右栏压成 **2 Tab**（锚点规则 / 全局规则），并把**文档类型**提到顶栏
 *   （按钮组，图片 / PDF 不可解析 ⇒ 置灰锁定）。因此本组件：
 *   - ⛔ **删掉**「两步引导条」与「文档类型卡片网格」—— 类型已上移顶栏，
 *     留着就是**同一件事两处入口**（改一处另一处不同步，用户不知道哪个算数）；
 *   - ✅ 保留「文档属性」（角色 / 作用 / 业务标签 / 关键信息项）
 *     一块，作为「全局规则」Tab 的第一块内容；
 *   - ✅ 新增 `fixed` 文档的「是否可替换」（`FixedDocSubtype`，D-AA1 已裁）。
 *
 * 【★ 2026-10-06 用户裁决：再删两块】
 *   ① 「文档名称」—— 与文件名同源（`DocName` 回落 `FileName`），人工改它没有意义
 *     ⇒ ⛔ 不再渲染输入框，但 `form.DocName` **保留**（保存仍回传加载值，契约不缺列）；
 *   ② 「AI 识别溯源」整块 —— 内容（模型 / 置信度 / 来源）与进度条、
 *     「开始 AI 语义分析」按钮重复，且置信度曾渲染成 `0.93%` 的口径错误
 *     ⇒ ⛔ 整块删除，状态看顶部进度条与已带出的字段本身。
 *
 * 【★ 2026-10-07 用户裁决：单块拆成两块分组卡片（第二轮改造之②）】
 *   「全局规则」Tab 排三块纵向卡片：**分组 / 文档作用 / 全局填写规则**。
 *   本组件承载前两块（同一行契约，共用底部「保存文档属性」）：
 *   - 卡1 **分组**：文档角色 + 业务标签 + 关键信息项（这个文档属于哪一类、要哪些信息）；
 *   - 卡2 **文档作用**：作用 textarea + `fixed` 的是否可替换（这个文档干什么用）。
 *   卡3「全局填写规则」由父页 `index.vue` 渲染（`PromptPanel`，仅锚点含 ai 节点时出现）。
 *
 * 【为什么「可替换性」放在这里而不是锚点页】
 *   它是**文档级**属性（`cert_standard_doc_contract.FixedDocSubtype`），
 *   与锚点无关 ⇒ 归「全局规则」。
 */
import { Document, RefreshRight } from '@element-plus/icons-vue'
import {
  saveDocContract,
  type DocContractDetail,
} from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhEmptyState } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import { labelOf } from './contractLabels'

const props = defineProps<{
  detail: DocContractDetail | null
  loading?: boolean
  /**
   * 文档类型是否被**锁死**（图片 / PDF 不可解析）。
   * 锁死时「是否可替换」仍需人工判断（它答的是「企业要不要交」，
   * 与「能不能解析」无关），故本组件只把该事实透传给 `fixed` 分支的提示文案。
   */
  typeLocked?: boolean
}>()

const emit = defineEmits<{
  (e: 'saved'): void
  (e: 'reload'): void
}>()

const saving = ref(false)

/* ============ 编辑态 ============ */
const form = ref({
  DocCategory: 'editable',
  DocRole: 'required',
  /** ⚠️ **不再渲染输入框**（2026-10-06 裁决：与文件名同源、冗余），但保存仍回传加载值 */
  DocName: '',
  DocPurpose: '',
  /** `fixed` 专用：`standard_provided` / `enterprise_provided` */
  FixedDocSubtype: 'enterprise_provided',
})
/** 元素可能是 AI 落库的**对象**（标签 `{tagCode,tagName,…}`、信息项 `{Name,Required,…}`），也可能是人工新增的纯字符串 */
const tags = ref<unknown[]>([])
const infoItems = ref<unknown[]>([])
const newTag = ref('')
const newItem = ref('')

const DOC_ROLES = [
  { value: 'required', label: '必需提供' },
  { value: 'optional', label: '可选提供' },
  { value: 'reference', label: '参考资料' },
  { value: 'attachment', label: '附件' },
]

/**
 * `fixed` 文档的「可替换性」（49-V3 §2.2 已裁 D-AA1：**人工判断，⛔ 程序不推导**）。
 *
 * ⚠️ 取值口径是 `standard_provided` / `enterprise_provided`。
 *   DDL 注释里曾写 `platform_generated` —— **那是错的**（两个不同的业务维度：
 *   「谁生产」vs「能不能被替换」），别照着抄。
 */
const SUBTYPE_OPTIONS = [
  {
    value: 'standard_provided',
    label: '标准自带',
    desc: '标准里本来就有，不向企业索取',
  },
  {
    value: 'enterprise_provided',
    label: '企业提供',
    desc: '要企业交上来，需要匹配依据',
  },
]

/** 生效类型：不可解析 ⇒ 恒 `fixed`（与 `logic.effectiveDocCategory` 同一口径） */
const isFixed = computed(
  () => !!props.typeLocked || form.value.DocCategory === 'fixed',
)

function parseArray(raw?: string | null): unknown[] {
  const t = (raw ?? '').trim()
  if (!t) return []
  try {
    const v = JSON.parse(t)
    return Array.isArray(v) ? v : []
  } catch {
    return []
  }
}

watch(
  () => props.detail,
  (d) => {
    if (!d) return
    form.value = {
      DocCategory: d.DocCategory || 'editable',
      DocRole: d.DocRole || 'required',
      DocName: d.DocName || d.FileName || '',
      DocPurpose: d.DocPurpose || '',
      FixedDocSubtype: d.FixedDocSubtype || 'enterprise_provided',
    }
    tags.value = parseArray(d.TagsJson)
    infoItems.value = parseArray(d.InfoItemsJson)
  },
  { immediate: true },
)

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

function pickSubtype(v: string) {
  form.value.FixedDocSubtype = v
}

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
        TagsJson: JSON.stringify(tags.value),
        InfoItemsJson: JSON.stringify(infoItems.value),
        // ⛔ 非 fixed 文档不提交该列 —— 后端只认 fixed 分支，发了也是噪音
        FixedDocSubtype: isFixed.value ? form.value.FixedDocSubtype : null,
      }),
      '保存文档契约失败',
    )
    ElMessage.success('文档属性已保存')
    emit('saved')
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

defineExpose({ onSave })
</script>

<template>
  <div class="contract-tab" v-loading="loading">
    <template v-if="detail">
      <!-- ── 卡1 · 分组（文档角色 + 业务标签 + 关键信息项）── -->
      <div class="blk">
        <div class="blk-hd">分组</div>
        <div class="blk-bd">
          <div class="fld">
            <!-- ⚠️ `el-select` 的 `id` 落到**内部隐藏 input**（`select2.mjs:201`），
                 ⛔ 不是 `role="combobox"` 那个元素 ⇒ `for/id` 会关联到错的节点，
                 只能用 `aria-label`（`select2.mjs:217` 绑到 combobox）。 -->
            <label>文档角色</label>
            <el-select
              v-model="form.DocRole"
              aria-label="文档角色"
              style="width: 100%"
            >
              <el-option
                v-for="r in DOC_ROLES"
                :key="r.value"
                :value="r.value"
                :label="r.label"
              />
            </el-select>
          </div>

          <div class="fld">
            <label id="cf-doc-tags-label">业务标签</label>
            <div
              class="tags-list"
              role="group"
              aria-labelledby="cf-doc-tags-label"
            >
              <el-tag
                v-for="(t, i) in tags"
                :key="i"
                closable
                size="small"
                effect="plain"
                class="m-tag"
                @close="removeTag(i)"
              >
                {{ labelOf(t) }}
              </el-tag>
              <el-input
                v-model="newTag"
                aria-label="新增业务标签"
                class="add-tag-input"
                size="small"
                placeholder="+ 新增"
                @keyup.enter="addTag"
                @blur="addTag"
              />
            </div>
          </div>

          <div class="fld" style="margin-bottom: 0">
            <label id="cf-doc-items-label">包含的关键信息项</label>
            <div
              class="tags-list"
              role="group"
              aria-labelledby="cf-doc-items-label"
            >
              <el-tag
                v-for="(t, i) in infoItems"
                :key="i"
                closable
                size="small"
                effect="plain"
                class="m-tag"
                @close="removeItem(i)"
              >
                {{ labelOf(t) }}
              </el-tag>
              <el-input
                v-model="newItem"
                aria-label="新增关键信息项"
                class="add-tag-input"
                size="small"
                placeholder="+ 新增项"
                @keyup.enter="addItem"
                @blur="addItem"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- ── 卡2 · 文档作用（作用 textarea + fixed 的是否可替换）── -->
      <div class="blk">
        <div class="blk-hd">文档作用</div>
        <div class="blk-bd">
          <div class="fld">
            <label for="cf-doc-purpose">文档作用</label>
            <el-input
              id="cf-doc-purpose"
              v-model="form.DocPurpose"
              type="textarea"
              :rows="4"
              placeholder="这份文档在认证流程中起什么作用、审核关注什么…"
            />
          </div>

          <!-- ★ fixed 专用：是否可替换（人工判断，D-AA1） -->
          <div v-if="isFixed" class="fld" style="margin-bottom: 0">
            <label>是否可替换</label>
            <div class="typegrid">
              <div
                v-for="s in SUBTYPE_OPTIONS"
                :key="s.value"
                class="tcard"
                :class="{ on: form.FixedDocSubtype === s.value }"
                role="button"
                tabindex="0"
                :aria-pressed="form.FixedDocSubtype === s.value"
                @click="pickSubtype(s.value)"
                @keydown.enter.prevent="pickSubtype(s.value)"
                @keydown.space.prevent="pickSubtype(s.value)"
              >
                <div class="tt">
                  <span>{{ s.label }}</span>
                </div>
                <div class="td">{{ s.desc }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="row" style="justify-content: flex-end">
        <el-button
          type="default"
          :icon="RefreshRight"
          size="small"
          @click="emit('reload')"
          >重新读取</el-button
        >
        <el-button
          type="primary"
          size="small"
          :loading="saving"
          @click="onSave"
          >保存文档属性</el-button
        >
      </div>
    </template>

    <YzhEmptyState v-else :icon="Document" title="请先在左侧选择一个文档" />
  </div>
</template>

<style scoped>
.contract-tab {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

/* V6 骨架回归 */
.blk {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-md, 8px);
  margin-bottom: var(--yzh-space-3, 14px);
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
}
.blk-hd {
  background: var(--yzh-color-bg-subtle, #f9fafb);
  padding: var(--yzh-space-2, 9px) var(--yzh-space-3, 14px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 500;
  color: var(--yzh-color-text-primary, #303133);
  display: flex;
  align-items: center;
  gap: 8px;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.blk-bd {
  padding: var(--yzh-space-3, 14px);
}

/* 类型 / 可替换性选择网格 */
.typegrid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.tcard {
  border: 1.5px solid var(--yzh-color-border-light, #e4e7ed);
  border-radius: var(--yzh-radius-md, 8px);
  padding: var(--yzh-space-3, 14px);
  cursor: pointer;
  background: var(--yzh-color-bg-container, #fff);
  transition: all var(--yzh-transition-base, 200ms);
}
.tcard:hover {
  border-color: var(--yzh-color-primary-light-7, #c6e2ff);
  background: var(--yzh-color-primary-light-9, #fbfdff);
}
.tcard.on {
  border-color: var(--yzh-color-primary, #409eff);
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  box-shadow: 0 0 0 2px var(--yzh-color-primary-light-8, #d9ecff);
}
.tcard .tt {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: var(--yzh-space-2, 6px);
}
.tcard .td {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 1.7;
}
.tcard.on .tt {
  color: var(--yzh-color-primary-dark-2, #337ecc);
}
/* 卡片是 role=button ⇒ 键盘焦点必须可见 */
.tcard:focus-visible {
  outline: 2px solid var(--yzh-color-primary, #1e3a8a);
  outline-offset: 1px;
}

/* 表单与标签 */
.fld {
  margin-bottom: var(--yzh-space-3, 12px);
}
.fld label {
  display: block;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  margin-bottom: var(--yzh-space-1, 5px);
}
.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: var(--yzh-space-2, 8px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-radius: var(--yzh-radius-sm, 4px);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.m-tag {
  border-radius: var(--yzh-radius-sm, 4px);
}
.add-tag-input {
  width: 80px;
}
.add-tag-input :deep(.el-input__inner) {
  height: 24px;
  padding: 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 11px);
}

.row {
  display: flex;
  align-items: center;
  gap: 8px;
}
.wrap {
  flex-wrap: wrap;
}
</style>
