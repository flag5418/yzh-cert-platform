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
 *   - ✅ 新增 `fixed` 文档的「是否可替换」——
 *     ⚠️ **2026-10-16 已改名并放宽适用范围**，见下方「2026-10-16 大改」；
 *     权威列由 `cert_standard_doc_contract.FixedDocSubtype` 改为
 *     `cert_standard_directory_file.Replaceable`。
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
 *   它是**文档级**属性，与锚点无关 ⇒ 归「全局规则」。
 *
 * 【★ 2026-10-16 大改：文档角色 → 文档约束（`67` 号 §5）】
 *   ① ⛔ **删除「文档角色」**（旧 4 值：必须 / 可选 / 参考 / 附件）—— 实测它在真实
 *      规范化链路（`DocumentFillOrchestrator`）里**零引用**；唯一被读的地方是
 *      「生成提示词」时把**英文枚举码**拼进提示词文本（`【文档角色】required`）。
 *      ⇒ 收敛为「**文档约束**」2 值：必要文档 / 非必要文档。
 *   ② 「是否可替换」→ 改名「**是否依赖企业文档**」，并**去掉 `v-if="isFixed"` 限制** ——
 *      该维度对**全部文档**有效，与「文档类型」**正交**（标准里的导航页 / 固定描述
 *      通常是 `editable`，但内容不依赖企业资料）。
 *   ③ 两个字段的**权威列都在 `cert_standard_directory_file`（标准行）**，
 *      ⛔ 不是契约表 —— 契约表的 `DocRole` / `FixedDocSubtype` 已废弃
 *      （「第三对分叉列」：页面写契约列、编排器读目录列 ⇒ 配了不生效）。
 *   ④ 布局重划为两卡：**文档约束**（怎么对待这份文档）/ **文档描述**（这份文档是什么）。
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
   *
   * ⚠️ 2026-10-16：本组件**已不再消费**它 —— 旧实现用它把「是否可替换」锁在 `fixed` 分支，
   * 而新口径下「是否依赖企业文档」对全部文档有效。prop 保留是为了不破坏父页调用契约
   * （`index.vue` 仍在传 `:type-locked`），⛔ 不要删（删了父页会报未声明 prop）。
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
  /** ⚠️ **不再渲染输入框**（2026-10-06 裁决：与文件名同源、冗余），但保存仍回传加载值 */
  DocName: '',
  DocPurpose: '',
  /** ★ 文档约束（`67` §4.2）：`true` = 必要文档 / `false` = 非必要文档 */
  IsRequired: true,
  /** ★ 是否依赖企业文档（`67` §4.2）：`true` = 依赖 / `false` = 不依赖（标准自带） */
  Replaceable: true,
})
/** 元素可能是 AI 落库的**对象**（标签 `{tagCode,tagName,…}`、信息项 `{Name,Required,…}`），也可能是人工新增的纯字符串 */
const tags = ref<unknown[]>([])
const infoItems = ref<unknown[]>([])
const newTag = ref('')
const newItem = ref('')

/**
 * ★ **文档约束**（`67` 号 §4.2，用户 2026-10-16 裁决②）。
 *
 * 由旧「文档角色」的 4 值收敛为 2 值 —— 旧值里 `reference` / `attachment` 在
 * 企业资料规范化里**从未有过业务含义**，且整列在真实链路里零引用。
 */
const CONSTRAINT_OPTIONS = [
  {
    value: true,
    label: '必要文档',
    desc: '认证必须有这份文档；没生成出来要计入缺口',
  },
  {
    value: false,
    label: '非必要文档',
    desc: '有更好；生成不出来可以忽略，不计缺口',
  },
]

/**
 * ★ **是否依赖企业文档**（`67` 号 §4.2，用户 2026-10-16 裁决①：
 * 与旧的「是否可替换」是**同一件事**）。
 *
 * ⚠️ 对**全部文档**有效，与「文档类型」**正交** —— 旧实现只在固定文档时显示，是错的。
 */
const DEPENDS_OPTIONS = [
  {
    value: true,
    label: '依赖企业文档',
    desc: '内容来自企业交上来的资料，系统要去匹配',
  },
  {
    value: false,
    label: '不依赖企业文档',
    desc: '标准自带的内容（导航页、固定描述），企业不用交，系统自动生成',
  },
]

/** 选「不依赖企业文档」时给一行提示（⛔ 非阻断，见 `67` §6.2） */
const showDependsHint = computed(() => form.value.Replaceable === false)

/**
 * ★ **保存时后端回传的配置期警告**（`67` §6.2，2026-10-16）。
 *
 * 语义：该文档标了「不依赖企业文档」，但**某些锚点的取值来源需要企业资料**
 * （如 `ai_field` 从企业文档抽字段 / `enterprise.*` 读企业档案）⇒ 运行期会取不到值。
 *
 * ⚠️ 与上面那行静态提示的区别：这行是**扫描真实锚点配置**得出的结论，能指出具体是哪个锚点；
 *    上面那行只是「你选了这个开关」的通用提醒。
 *
 * ⛔ 不阻断保存 —— 用户可能是「先配锚点 → 后改文档属性」，阻断会把人卡在中间态。
 */
const saveWarnings = ref<string[]>([])

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
      DocName: d.DocName || d.FileName || '',
      DocPurpose: d.DocPurpose || '',
      // ★ 权威值来自**标准目录行**（后端 `Detail` 已改为从文件行取），默认 true
      IsRequired: d.IsRequired ?? true,
      Replaceable: d.Replaceable ?? true,
    }
    tags.value = parseArray(d.TagsJson)
    infoItems.value = parseArray(d.InfoItemsJson)
    // ★ 换了文档 ⇒ 上一次的警告必须清掉 —— 否则会把 A 文档的结论挂在 B 文档上
    //   （本项目最忌讳的「看起来有结论，实际是别的对象的」）。
    saveWarnings.value = []
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

function pickConstraint(v: boolean) {
  form.value.IsRequired = v
}
function pickDepends(v: boolean) {
  form.value.Replaceable = v
}

async function onSave() {
  const fileCode = props.detail?.StandardFileCode
  if (!fileCode) return
  saving.value = true
  try {
    const saved = unwrapOk(
      await saveDocContract({
        StandardFileCode: fileCode,
        DocName: form.value.DocName,
        DocCategory: form.value.DocCategory,
        // ★ 文档约束 + 是否依赖企业文档（2026-10-16）——
        //   后端同批写 `cert_standard_directory_file`（**标准行**），⛔ 不是契约表
        IsRequired: form.value.IsRequired,
        Replaceable: form.value.Replaceable,
        DocPurpose: form.value.DocPurpose,
        TagsJson: JSON.stringify(tags.value),
        InfoItemsJson: JSON.stringify(infoItems.value),
      }),
      '保存文档契约失败',
    )
    // ★ 保存成功后再刷新警告（⛔ 失败时不清空 —— 让用户还能看到上一次的结论）
    saveWarnings.value = saved?.Warnings ?? []
    if (saveWarnings.value.length > 0) {
      // ⚠️ 用 warning 而非 success —— 保存**确实成功了**，但有配置矛盾要提醒
      ElMessage.warning(`文档属性已保存，但有 ${saveWarnings.value.length} 处配置需要确认`)
    } else {
      ElMessage.success('文档属性已保存')
    }
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
      <!-- ── 卡1 · 文档约束（怎么对待这份文档）── -->
      <div class="blk">
        <div class="blk-hd">文档约束</div>
        <div class="blk-bd">
          <div class="fld">
            <label>文档约束</label>
            <div class="typegrid">
              <div
                v-for="c in CONSTRAINT_OPTIONS"
                :key="c.label"
                class="tcard"
                :class="{ on: form.IsRequired === c.value }"
                role="button"
                tabindex="0"
                :aria-pressed="form.IsRequired === c.value"
                @click="pickConstraint(c.value)"
                @keydown.enter.prevent="pickConstraint(c.value)"
                @keydown.space.prevent="pickConstraint(c.value)"
              >
                <div class="tt">
                  <span>{{ c.label }}</span>
                </div>
                <div class="td">{{ c.desc }}</div>
              </div>
            </div>
          </div>

          <div class="fld">
            <label>是否依赖企业文档</label>
            <div class="typegrid">
              <div
                v-for="o in DEPENDS_OPTIONS"
                :key="o.label"
                class="tcard"
                :class="{ on: form.Replaceable === o.value }"
                role="button"
                tabindex="0"
                :aria-pressed="form.Replaceable === o.value"
                @click="pickDepends(o.value)"
                @keydown.enter.prevent="pickDepends(o.value)"
                @keydown.space.prevent="pickDepends(o.value)"
              >
                <div class="tt">
                  <span>{{ o.label }}</span>
                </div>
                <div class="td">{{ o.desc }}</div>
              </div>
            </div>
            <!-- ★ 非阻断提示（`67` §6.2）：不依赖企业资料 ⇒ 锚点来源别配企业资料类 -->
            <div v-if="showDependsHint" class="hint">
              该文档不依赖企业资料 —— 它的锚点取值来源不要配置为「企业资料」类，否则会取不到值。
            </div>
            <!-- ★★ 保存后由后端**扫描真实锚点配置**得出的具体警告（⛔ 不阻断，仅提示） -->
            <el-alert
              v-if="saveWarnings.length > 0"
              class="hint-alert"
              type="warning"
              :closable="false"
              show-icon
              title="保存成功，但以下配置会取不到值"
            >
              <ul class="hint-list">
                <li v-for="(w, i) in saveWarnings" :key="i">{{ w }}</li>
              </ul>
            </el-alert>
          </div>

          <div class="fld" style="margin-bottom: 0">
            <label for="cf-doc-purpose">文档作用</label>
            <el-input
              id="cf-doc-purpose"
              v-model="form.DocPurpose"
              type="textarea"
              :rows="4"
              placeholder="这份文档在认证流程中起什么作用、审核关注什么…"
            />
          </div>
        </div>
      </div>

      <!-- ── 卡2 · 文档描述（这份文档是什么）── -->
      <div class="blk">
        <div class="blk-hd">文档描述</div>
        <div class="blk-bd">
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

/* ★ 非阻断提示（`67` §6.2）：选「不依赖企业文档」时出现。
   ⚠️ 全部用令牌 + 兜底（R18），⛔ 不写裸 hex / 裸 px。 */
.hint {
  margin-top: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  color: var(--yzh-color-text-regular, #606266);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-radius: var(--yzh-radius-sm, 4px);
}

/* ★ 保存后由后端扫描得出的**具体**警告（`67` §6.2）。
   ⚠️ 与 .hint 的区别：.hint 是「你选了这个开关」的通用提醒，
   .hint-alert 是「哪个锚点配错了」的具体结论 ⇒ 用 el-alert 而非灰底 div，
   让它在一屏里**能被一眼看见**（通用提示看多了会被无视）。 */
.hint-alert {
  margin-top: var(--yzh-space-2, 8px);
}
.hint-list {
  margin: 0;
  padding-left: var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
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
