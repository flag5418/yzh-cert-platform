<script setup lang="ts">
/**
 * 锚点规则 —— 把「空白模板里扫出来的 {{标签}}」与「数据源」绑起来。
 *
 * 【★ 2026-10-05 第 28 轮（C4 / C7 / C8）】
 *   - **C4 按字段 / 表格分组**：此前是一长条平铺列表，字段锚点与表格锚点混在一起，
 *     用户得逐条读 `AnchorType` 才知道这条要不要填一整张表。现在分成两组，组头带条数。
 *   - **C7 锁定**：行内一个锁定开关（走 `DocTemplateAnchor/lock`）。
 *     锁定 = 实施人员认可了这份配置 ⇒ **后端会拒绝再改配置列**，
 *     所以锁定后「配置规则」改为只读提示，⛔ 不让用户白改一遍再被打回。
 *   - ~~**C8 未配齐警示**：锚点没配完 ⇒ 顶部一条警示 + 徽标~~
 *     **2026-10-07 第二轮之④已删警示条**（用户裁决：错误改「Tab 角标 + 点击弹层」，
 *     ⛔ 不让提示占太多空间）。明细在右栏角标弹层（`index.vue` 的 `anchorIssues`，
 *     同源于 `anchorViews`）；本页统计条的「已配齐 / 未配齐」一行徽标保留。
 *     「自动填充 / 预览」按钮仍在右栏操作条，按 `logic.anchorReadiness.ready` 置灰。
 *
 * 【★ 锚点清单的存储位置（本轮上移）】
 *   清单由 `logic.anchorRows` 持有（`logic.reloadAnchors()` 加载）—— 不再由本组件
 *   自持 `rows`。原因：默认落「全局规则」Tab 时本组件**未挂载**，
 *   而底部保存条 / Tab 徽标 / 中栏都要读「锚点是否配齐」。
 *   放在组件里会导致「没点过锚点页 ⇒ 一律显示未配齐」。
 */
import { Filter, Lock, Unlock } from '@element-plus/icons-vue'
import { lockAnchor } from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import {
  ANCHOR_QUICK_FILTERS,
  anchorBadge,
  filterAnchorViews,
  filterCount,
  type AnchorQuickFilter,
  type AnchorView,
} from './anchorStats'
import AnchorSidePanel from './AnchorSidePanel.vue'

const props = defineProps<{
  logic: any
  templateCode: string
  hasTemplate: boolean
  scanStatus?: string
}>()

const emit = defineEmits<{ (e: 'saved'): void }>()

const panelRef = ref<any>(null)
/** 当前展开配置抽屉的锚点**整行**（`AnchorSidePanel` 需要原始 PascalCase 行） */
const currentAnchor = ref<any>(null)
/** 配置抽屉开关 */
const panelVisible = ref(false)
/** 正在保存「必填」开关的那一行 Code —— 只让那一行转圈，⛔ 不全表禁用 */
const pendingCode = ref('')
/** 正在切换锁定的那一行 Code */
const lockPending = ref('')
const quickFilter = ref<AnchorQuickFilter>('all')

/* ============ 派生（全部走 logic 的共享仓库） ============ */
const views = computed<AnchorView[]>(() => props.logic.anchorViews)
const stats = computed(() => props.logic.anchorStats)
const readiness = computed(() => props.logic.anchorReadiness)
const filtered = computed(() => filterAnchorViews(views.value, quickFilter.value))

/** ★ C4：表格类锚点（`table` / `table_total`）与字段类分开 */
const TABLE_TYPES = ['table', 'table_total']
const isTableRow = (v: AnchorView) =>
  TABLE_TYPES.includes(String(v.row?.AnchorType ?? ''))

const fieldViews = computed(() => filtered.value.filter((v) => !isTableRow(v)))
const tableView = computed(() => filtered.value.filter(isTableRow))
/** 已锁定的条数（原型 V1：行头一个「N 个已锁定」徽标） */
const lockedCount = computed(
  () => views.value.filter((v) => !!v.row?.IsLocked).length,
)

/* ============ 加载 ============ */
/**
 * 强制重拉（数据确实变了：抽屉保存成功 / 父页扫描后调用）。
 * ⛔ 挂载兜底不要用它 —— 那是 `ensureAnchors()`（幂等）。
 */
function loadAll() {
  return props.logic.reloadAnchors()
}

/** 供父页在「重新扫描 / 上传模板」后调用 */
async function refresh() {
  await loadAll()
}

defineExpose({ refresh })

/**
 * ★ 首次挂载：**幂等**兜底加载。
 *
 * 【为什么要这个兜底】
 *   清单的**主**加载在父页（`handleNodeClick`）—— 因为默认落「全局规则」时
 *   本组件**未挂载**，而 Tab 徽标 / C8 闸都要读它。
 *   但本组件仍不该假设「一定有人先拉过」：若父页那次请求失败、或将来有人单独挂载它，
 *   页面会**静默空白**（不报错、也没有加载态）。
 *
 * 【★ 2026-10-06 评审 #2：判据从「仓库为空」改成「本模板没加载过」】
 *   原判据 `!anchorRows.length` 有个反例：**该模板本来就没有锚点**时，仓库合法地是空的，
 *   于是每次切回锚点页签都会**再打一次接口**。
 *   `ensureAnchors()` 用 `anchorLoadedFor` 标记去重 ⇒ 「没拉过才拉」，与条数无关。
 */
onMounted(() => {
  void props.logic.ensureAnchors()
})

watch(
  () => props.templateCode,
  () => {
    currentAnchor.value = null
    panelVisible.value = false
    quickFilter.value = 'all'
    loadAll()
  },
)

/* ============ 交互 ============ */
function onRowClick(v: AnchorView) {
  currentAnchor.value = v.row
  panelVisible.value = true
}

/**
 * 抽屉关完（离场动画结束）后清掉当前锚点。
 *
 * ⚠️ 必须在 `closed` 而不是 `close` 里清：`close` 是**立即**触发的，
 *    那时把 `anchor` 置空会让抽屉在滑出的 300ms 里**内容先消失**（肉眼可见的闪空）。
 * ⚠️ 又必须清：否则「点 A → 取消 → 再点 A」时 `anchor` 引用没变，
 *    `AnchorSidePanel` 的 watch 不触发 ⇒ 面板里残留上一次的未保存编辑。
 */
function onPanelClosed() {
  currentAnchor.value = null
}

function onSaved() {
  emit('saved')
  loadAll()
}

/**
 * 「必填项」就地开关。
 *
 * ⚠️ 走 `logic.updateAnchor(row)`，它按 `uk_tpl_anchor` **整行 upsert** ——
 *    所以这里提交的必须是**从 `loadAnchors()` 拿到的原始整行**，
 *    ⛔ 不能只传 `{ Code, Required }`（其余列会被写成 CLR 默认值）。
 */
async function toggleRequired(v: AnchorView) {
  const row = v.row
  if (row.IsLocked) {
    ElMessage.warning('该锚点已锁定 —— 请先解锁再修改配置')
    return
  }
  const next = !row.Required
  pendingCode.value = String(row.Code ?? '')
  row.Required = next // 乐观更新，失败回滚
  try {
    await props.logic.updateAnchor(row)
    ElMessage.success(`「${row.AnchorRef}」已设为${next ? '必填' : '非必填'}`)
    emit('saved')
  } catch (e: any) {
    row.Required = !next
    ElMessage.error(e?.message || '保存失败')
  } finally {
    pendingCode.value = ''
  }
}

/**
 * ★ C7：锁定 / 解锁单个锚点。
 *
 * 【★ 与「保存配置」正交】本动作**只改 `IsLocked`**；配置走 `save-batch`，
 *   两条通路在后端是分开的（`save-batch` 的列清单**刻意不含** `IsLocked`）
 *   ⇒ ⛔ 不要把锁定塞进「保存」里顺手做掉。
 * 【★ 锁定不设前置】锚点还没配来源也**允许**锁定 —— 后端只在 `Warnings` 里如实提示。
 *   是否配齐由 C8 闸与人工决定，⛔ 程序不阻断业务组合（P2'）。
 */
async function toggleLock(v: AnchorView) {
  const row = v.row
  const next = !row.IsLocked
  lockPending.value = String(row.Code ?? '')
  try {
    const res = await lockAnchor(props.templateCode, [String(row.Code)], next)
    const data = unwrapOk(res)
    row.IsLocked = next // 乐观更新（后端可能返回 Unchanged，但目标态一致）
    ElMessage.success(
      `${next ? '已锁定' : '已解锁'}「${row.AnchorRef}」` +
        (next ? ' —— 换模板重扫时会保留这份配置' : ''),
    )
    // ★ 后端如实推导的告警（如「锁定后仍不能用于自动填充」）必须转达，⛔ 不吞
    for (const w of data?.Warnings ?? []) ElMessage.warning(w)
    emit('saved')
  } catch (e: any) {
    ElMessage.error(e?.message || '锁定失败')
  } finally {
    lockPending.value = ''
  }
}
</script>

<template>
  <div class="anchor-tab">
    <YzhEmptyState
      v-if="!hasTemplate"
      title="还没有空白模板"
      description="先在右侧「上传空白模板」，系统据此扫描锚点"
    />

    <YzhEmptyState
      v-else-if="scanStatus !== 'completed'"
      title="该模板还没有扫描出锚点"
      description="锚点来自空白模板本身的声明（{{标签}} / 书签）"
    />

    <template v-else>
      <!--
        ★ 2026-10-07 第二轮之④：**删掉原 C8 顶部警示条**（用户裁决：
           「错误用角标显示，点击再展开详情，不要让提示信息占太多空间」）。
        未配 / 孤儿 / 解析失败明细已收进右栏「锚点规则」Tab 的**角标弹层**
        （`index.vue` 的 `anchorIssues`，数据同源于 `anchorViews` ⇒ 与本页徽标不矛盾）。
        ⚠️ 下面统计条里的「已配齐 / 未配齐」徽标保留 —— 它是一行紧凑状态，不是大块提示。
      -->

      <!-- 统计条 -->
      <div class="stat">
        <div class="s b"><b>{{ stats.total }}</b> 锚点</div>
        <div class="s s"><b>{{ stats.auto }}</b> 自动</div>
        <div class="s"><b>{{ stats.manual }}</b> 人工</div>
        <div v-if="stats.unconfigured" class="s d">
          <b>{{ stats.unconfigured }}</b> 未配
        </div>
        <div v-if="stats.staleRef" class="s d">
          <b>{{ stats.staleRef }}</b> 来源失效
        </div>
        <div v-if="stats.orphan" class="s w"><b>{{ stats.orphan }}</b> 孤儿</div>
        <span class="sp"></span>
        <YzhStatusBadge
          :type="readiness.ready ? 'success' : 'warning'"
          :text="readiness.ready ? '已配齐' : '未配齐'"
        />
        <YzhStatusBadge
          v-if="lockedCount"
          type="info"
          :text="`${lockedCount} 个已锁定`"
        />
      </div>

      <!-- 筛选器（用真按钮，可 Tab 聚焦 / Enter 触发） -->
      <div class="filters" role="group" aria-label="锚点筛选">
        <button
          v-for="f in ANCHOR_QUICK_FILTERS"
          :key="f.value"
          type="button"
          class="f"
          :class="{ on: quickFilter === f.value }"
          :aria-pressed="quickFilter === f.value"
          @click="quickFilter = f.value"
        >
          {{ f.label }}
          <span class="c">{{ filterCount(stats, f.value) }}</span>
        </button>
      </div>

      <!-- 锚点分组列表 -->
      <div v-loading="logic.isAnchorLoading" class="ac-list">
        <template v-if="filtered.length">
          <div
            v-for="g in [
              { title: '字段', rows: fieldViews },
              { title: '表格', rows: tableView },
            ]"
            :key="g.title"
            class="agroup"
          >
            <div class="ahd">
              <span>{{ g.title }}</span>
              <span class="cnt">{{ g.rows.length }} 个</span>
            </div>

            <div
              v-for="v in g.rows"
              :key="v.row.Code"
              class="ac"
              :class="{
                on: currentAnchor?.Code === v.row.Code,
                orphan: v.orphan,
                nosrc: v.unconfigured,
                locked: !!v.row.IsLocked,
              }"
              role="button"
              tabindex="0"
              :aria-pressed="currentAnchor?.Code === v.row.Code"
              @click="onRowClick(v)"
              @keydown.enter.prevent="onRowClick(v)"
              @keydown.space.prevent="onRowClick(v)"
            >
              <div class="ac-hd">
                <span class="ref">{{ v.row.AnchorRef }}</span>
                <span class="sp"></span>
                <YzhStatusBadge
                  :type="anchorBadge(v).tone"
                  :text="anchorBadge(v).text"
                />
              </div>

              <div class="ac-meta">
                <span class="m">类型：<b>{{ v.row.AnchorType }}</b></span>
                <span class="m">位置：<b>{{ v.row.Position || '正文' }}</b></span>
                <span v-if="v.parseError" class="m m--bad">来源配置无法解析</span>
              </div>

              <div class="ac-src">
                <span class="k">来源:</span>
                <span class="v" :title="v.summary">{{
                  v.summary || '未配置'
                }}</span>
              </div>

              <div class="ac-op" @click.stop>
                <span class="sw-label">必填项</span>
                <el-switch
                  :model-value="!!v.row.Required"
                  size="small"
                  :disabled="!!v.row.IsLocked"
                  :loading="pendingCode === String(v.row.Code)"
                  :aria-label="`${v.row.AnchorRef} 是否必填`"
                  @change="toggleRequired(v)"
                />
                <span class="sp"></span>
                <!-- ★ C7：锁定开关（⛔ 不冒泡到「打开设置页」） -->
                <el-button
                  link
                  :type="v.row.IsLocked ? 'primary' : 'default'"
                  size="small"
                  :icon="v.row.IsLocked ? Lock : Unlock"
                  :loading="lockPending === String(v.row.Code)"
                  :title="
                    v.row.IsLocked
                      ? '已锁定 —— 这份配置已确定，不能再修改；换模板重扫时会保留'
                      : '锁定（表示这个字段的配置已确定）'
                  "
                  @click="toggleLock(v)"
                >
                  {{ v.row.IsLocked ? '已锁定' : '锁定' }}
                </el-button>
                <el-button
                  link
                  type="primary"
                  size="small"
                  @click="onRowClick(v)"
                  >{{ v.row.IsLocked ? '查看' : '配置规则' }}</el-button
                >
              </div>
            </div>

            <div v-if="!g.rows.length" class="agroup-empty">
              没有{{ g.title }}锚点
            </div>
          </div>
        </template>

        <YzhEmptyState
          v-else
          compact
          :icon="Filter"
          :icon-size="32"
          title="该筛选条件下暂无锚点"
          description="可以尝试切换上方筛选条件"
        />
      </div>

      <!-- 右侧抽屉：锚点规则编辑（外壳由 `YzhDrawer` 统一提供） -->
      <AnchorSidePanel
        ref="panelRef"
        v-model:visible="panelVisible"
        :anchor="currentAnchor"
        :template-code="templateCode"
        @saved="onSaved"
        @closed="onPanelClosed"
      />
    </template>
  </div>
</template>

<style scoped>
.anchor-tab {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

/* ★ 2026-10-07：原 `.warnbar`（C8 未配齐警示条）已删 —— 明细改由右栏
   Tab 角标弹层承载（用户裁决之④：⛔ 不让提示信息占太多空间）。 */

/* 统计条 */
.stat {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 6px);
  flex-wrap: wrap;
  margin-bottom: var(--yzh-space-2, 10px);
}
.stat .s {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-sm, 4px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 9px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  background: var(--yzh-color-bg-subtle, #fafafa);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 5px);
}
.stat .s b {
  font-weight: 600;
}
.stat .s.b b {
  color: var(--yzh-color-primary, #1e3a8a);
}
.stat .s.s b {
  color: var(--yzh-color-success, #16a34a);
}
.stat .s.w b {
  color: var(--yzh-color-warning, #d97706);
}
.stat .s.d b {
  color: var(--yzh-color-danger, #dc2626);
}

/* 筛选器 —— 真按钮：清掉 UA 默认样式，保留键盘可达性 */
.filters {
  display: flex;
  gap: var(--yzh-space-1, 6px);
  margin-bottom: var(--yzh-space-3, 12px);
  flex-wrap: wrap;
}
.filters .f {
  padding: var(--yzh-space-1, 3px) var(--yzh-space-2, 10px);
  font-size: var(--yzh-font-size-xs, 12px);
  font-family: inherit;
  line-height: 1.6;
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-lg, 12px);
  cursor: pointer;
  color: var(--yzh-color-text-regular, #606266);
  background: var(--yzh-color-bg-container, #fff);
  transition: all var(--yzh-transition-fast, 150ms);
}
.filters .f:hover {
  border-color: var(--yzh-color-primary-light-8, #d9ecff);
  color: var(--yzh-color-primary, #1e3a8a);
}
.filters .f.on {
  background: var(--yzh-color-primary, #1e3a8a);
  border-color: var(--yzh-color-primary, #1e3a8a);
  color: var(--yzh-color-bg-container, #fff);
}
.filters .f .c {
  opacity: 0.72;
  margin-left: var(--yzh-space-1, 3px);
}

/* 列表容器 */
.ac-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
  padding-right: var(--yzh-space-1, 4px);
}

/* ★ C4 分组 */
.agroup .ahd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 7px);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
  padding: 0 var(--yzh-space-1, 2px) var(--yzh-space-1, 7px);
}
.agroup .ahd .cnt {
  font-weight: 400;
  color: var(--yzh-color-text-placeholder, #909399);
  font-size: var(--yzh-font-size-xs, 11px);
}
.agroup-empty {
  padding: var(--yzh-space-3, 14px);
  text-align: center;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}

/* 锚点卡片 */
.ac {
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-container, #fff);
  transition: all var(--yzh-transition-base, 200ms);
  overflow: hidden;
  cursor: pointer;
  margin-bottom: var(--yzh-space-2, 6px);
}
.ac:hover {
  border-color: var(--yzh-color-primary-light-7, #c6e2ff);
  box-shadow: var(--yzh-shadow-sm, 0 1px 3px rgba(0, 0, 0, 0.1));
}
/* 键盘焦点必须可见（卡片是 role=button） */
.ac:focus-visible {
  outline: 2px solid var(--yzh-color-primary, #1e3a8a);
  outline-offset: 1px;
}
.ac.on {
  border-color: var(--yzh-color-primary, #1e3a8a);
  box-shadow: 0 0 0 2px var(--yzh-color-primary-light-8, #d9ecff);
}
.ac.orphan {
  border-left: 3px solid var(--yzh-color-warning, #d97706);
}
.ac.nosrc {
  border-left: 3px solid var(--yzh-color-danger, #dc2626);
}
/* ★ C7 已锁定 —— 用主色左描边表达「已确认」 */
.ac.locked {
  border-left: 3px solid var(--yzh-color-primary, #1e3a8a);
}
.ac.locked .ac-op {
  background: var(--yzh-color-primary-light-9, #f4f8ff);
}

.ac-hd {
  padding: var(--yzh-space-2, 9px) var(--yzh-space-3, 12px);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  border-bottom: 1px dashed var(--yzh-color-border-light, #ebeef5);
}
.ac-hd .ref {
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-primary, #303133);
  background: var(--yzh-color-bg-subtle, #fafafa);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  padding: var(--yzh-space-1, 1px) var(--yzh-space-2, 7px);
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ac-meta {
  padding: var(--yzh-space-1, 7px) var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  display: flex;
  gap: var(--yzh-space-2, 10px);
  flex-wrap: wrap;
  border-bottom: 1px dashed var(--yzh-color-border-light, #ebeef5);
}
.ac-meta .m b {
  font-weight: 400;
  color: var(--yzh-color-text-placeholder, #c0c4cc);
}
.ac-meta .m--bad {
  color: var(--yzh-color-danger, #dc2626);
}

.ac-src {
  padding: var(--yzh-space-1, 7px) var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 6px);
  border-bottom: 1px dashed var(--yzh-color-border-light, #ebeef5);
  background: var(--yzh-color-bg-subtle, #fcfdff);
}
.ac-src .k {
  color: var(--yzh-color-text-placeholder, #909399);
  flex-shrink: 0;
}
.ac-src .v {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ac-op {
  padding: var(--yzh-space-1, 8px) var(--yzh-space-3, 12px);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 6px);
  background: var(--yzh-color-bg-subtle, #fafafa);
}
.sw-label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}

.sp {
  flex: 1;
}
</style>
