<script setup lang="ts">
/**
 * ★ 预览（右栏第三个 Tab）—— **自动填值 → 人工改 → 带 Overrides 出 PDF**。
 *
 * 【★ 用户裁决（2026-10-07 第二轮 4 项改造之③，前后端一起改）】
 *   ① 门槛 = **每个锚点规则都已填写** —— 判据直接用 `logic.anchorReadiness.ready`
 *      （与锚点页「自动填充」同一口径，⛔ 不在本组件另写一套）；
 *   ② 进入本 Tab 即**自动试填一次**，把每个锚点的当前值列出来逐条可改；
 *   ③ 点「生成预览 PDF」把改动作为 `Overrides[]` 带上重跑 → `previewed` 事件
 *      交给父页把产物路径写进共享仓库并切中栏「填充后预览」。
 *
 * 【★ 编辑框初值 = `Value`，⛔ 不是 `Display`】后端把 `Display` 截到 120 字符；
 *   拿 `Display` 回传会把长值**静默截断**后写进产物（第二次预览比第一次短一截，
 *   两边都不报错）—— 契约注释已把这条写死，见 `DocFillPreviewResult.Values`。
 *
 * 【与锚点页「自动填充」按钮的分工】同一个端点、同一条链路，区别只有
 *   `Overrides` 的有无：那颗 = 不改值直跑；本 Tab = 改完值再跑。
 *   ⛔ 本组件不写库、不发请求给别的端点，试填本身对业务数据**只读**。
 */
import { View } from '@element-plus/icons-vue'
import {
  runDocFillPreview,
  type DocFillPreviewResult,
} from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, inject, onMounted, ref, watch } from 'vue'
import type { DocFillRuleLogic } from '../logic'

type StatusType = 'success' | 'warning' | 'danger' | 'info'

const props = defineProps<{
  /** 空白模板 Code（无模板时整个 Tab 只显示门槛提示） */
  templateCode: string
  /**
   * 「为什么现在不能预览」—— 父页 `previewBlockReason` 传入，
   * 与「自动填充」按钮的 tooltip **同源**（⛔ 不在本组件复述一遍判据）。
   */
  blockReason: string
}>()

const emit = defineEmits<{
  /** 试填完成 ⇒ 父页写共享仓库 + 切中栏「填充后预览」 */
  (e: 'previewed', result: DocFillPreviewResult): void
}>()

const logic = inject<DocFillRuleLogic>('logic')

/** 门槛（唯一判据 = C8 的 `anchorReadiness.ready`） */
const ready = computed(() => !!props.templateCode && !!logic?.anchorReadiness.ready)

/** 试填值一行（初值来自首次 `Values`，改动只在本组件内存里） */
interface PreviewRow {
  AnchorRef: string
  Key: string
  Source: string
  Value: string
  /** 进入时的基准值 —— 与 `Value` 不等 = 用户改过（标「已改」） */
  Orig: string
}

const rows = ref<PreviewRow[]>([])
const lastStatus = ref<DocFillPreviewResult | null>(null)
const loading = ref(false)
const firing = ref(false)
/** 已自动取值的模板 Code（空 = 还没取过；⛔ 防止 ready 反复抖动时重复烧试填） */
const loadedFor = ref('')

const dirtyCount = computed(
  () => rows.value.filter((r) => r.Value !== r.Orig).length,
)

/**
 * 自动取值 —— 不带 `Overrides` 跑一次，拿 `Values` 当编辑初值。
 *
 * ⚠️ 这次跑也会产出一份 PDF（固定 key，会被随后带 Overrides 的那次覆盖）——
 * 可接受：同一条链路、同一个 key，用户永远看到的是最后一次结果。
 */
async function autofill() {
  if (!props.templateCode || !ready.value) return
  loading.value = true
  try {
    const d = unwrapOk(await runDocFillPreview(props.templateCode), '自动取值失败')
    loadedFor.value = props.templateCode
    lastStatus.value = d
    rows.value = (d.Values ?? []).map((v) => ({
      AnchorRef: v.AnchorRef,
      Key: v.Key || '',
      Source: v.Source || '',
      Value: v.Value ?? '',
      Orig: v.Value ?? '',
    }))
    if (d.Warnings?.length) ElMessage.warning(d.Warnings[0])
  } catch (e: any) {
    ElMessage.error(e?.message || '自动取值失败')
  } finally {
    loading.value = false
  }
}

/** 带 `Overrides` 重跑 ⇒ 产物 PDF 交给父页展示 */
async function onPreview() {
  if (!props.templateCode) return
  firing.value = true
  try {
    const overrides = rows.value.map((r) => ({
      AnchorRef: r.AnchorRef,
      Key: r.Key || undefined,
      Value: r.Value,
    }))
    const d = unwrapOk(
      await runDocFillPreview(props.templateCode, overrides),
      '预览生成失败',
    )
    lastStatus.value = d
    // 覆盖后的值就是新的基准
    rows.value.forEach((r) => (r.Orig = r.Value))
    emit('previewed', d)

    if (d.Status === 'filled') {
      ElMessage.success(`预览已生成：${d.AnchorCount} 个锚点全部写入`)
    } else if (d.Status === 'skipped_no_anchor') {
      ElMessage.warning('该模板没有锚点，产物 = 空白模板副本')
    } else {
      ElMessage.warning(
        `预览生成但未全部通过：待办 ${d.PendingCount} 处，自验收${d.Verified ? '通过' : '未通过'}`,
      )
    }
    if (d.Warnings?.length) ElMessage.warning(d.Warnings[0])
  } catch (e: any) {
    ElMessage.error(e?.message || '预览生成失败')
  } finally {
    firing.value = false
  }
}

/** 状态徽标语义（六态里只标三档，其余留 info） */
const statusMeta = computed<{ text: string; type: StatusType } | null>(() => {
  const d = lastStatus.value
  if (!d) return null
  if (d.Status === 'filled') return { text: `全绿 · ${d.AnchorCount} 处`, type: 'success' }
  if (d.Status === 'skipped_no_anchor') return { text: '无锚点', type: 'info' }
  if (d.Status === 'failed') return { text: '试填失败', type: 'danger' }
  return { text: `待办 ${d.PendingCount} 处`, type: 'warning' }
})

// 换文件 ⇒ 清空 + （门槛满足时）重新取值
watch(
  () => props.templateCode,
  () => {
    rows.value = []
    lastStatus.value = null
    loadedFor.value = ''
    if (ready.value) void autofill()
  },
)
// 门槛从不满足转为满足（锚点异步加载完）⇒ 补一次取值
watch(ready, (ok) => {
  if (ok && loadedFor.value !== props.templateCode) void autofill()
})
onMounted(() => {
  void logic?.ensureAnchors?.()
  if (ready.value) void autofill()
})
</script>

<template>
  <div class="pvtab">
    <!-- ── 门槛：锚点没配齐 ⇒ 只给一句事实（⛔ 不铺长段说明）── -->
    <YzhEmptyState
      v-if="!ready"
      compact
      :icon="View"
      title="暂不能预览"
      :description="blockReason || '锚点未配齐'"
    />

    <template v-else>
      <!-- 状态行：最近一次试填的结论 + 重新取值 -->
      <div class="pvtab__hd">
        <YzhStatusBadge v-if="statusMeta" :type="statusMeta.type" :text="statusMeta.text" />
        <span class="pvtab__meta">
          {{ rows.length }} 个值<template v-if="dirtyCount"> · 已改 {{ dirtyCount }} 处</template>
        </span>
        <span class="sp"></span>
        <!-- S04：按钮必须显式 type（default 也要写） -->
        <el-button type="default" size="small" :loading="loading" @click="autofill">
          重新取值
        </el-button>
      </div>

      <!-- 待办（锚点值取不到的处所 —— 如实列出，⛔ 不静默） -->
      <div v-if="lastStatus?.Pendings?.length" class="pvtab__pend">
        <div v-for="p in lastStatus.Pendings" :key="p.AnchorRef" class="pend">
          <b>{{ p.AnchorRef }}</b><span>{{ p.Reason }}</span>
        </div>
      </div>

      <!-- 可改值列表 -->
      <div v-loading="loading" class="pvtab__rows">
        <div v-for="r in rows" :key="r.AnchorRef" class="pvrow">
          <div class="pvrow__hd">
            <span class="pvrow__ref" :title="r.AnchorRef">{{ r.AnchorRef }}</span>
            <span v-if="r.Source" class="pvrow__src" :title="r.Source">{{ r.Source }}</span>
            <span v-if="r.Value !== r.Orig" class="pvrow__dirty">已改</span>
          </div>
          <el-input
            v-model="r.Value"
            type="textarea"
            :rows="2"
            size="small"
            :aria-label="`${r.AnchorRef} 的填写值`"
          />
        </div>

        <YzhEmptyState
          v-if="!rows.length && !loading"
          compact
          :icon="View"
          title="没有可填写的锚点值"
          description="模板里的锚点都取不到值"
        />
      </div>

      <!-- 动作行 -->
      <div class="pvtab__ft">
        <el-button
          type="primary"
          size="small"
          :loading="firing"
          :disabled="!rows.length"
          @click="onPreview"
        >
          生成预览 PDF
        </el-button>
        <span class="pvtab__hint">改完值点这里，产物在中栏「填充后预览」查看</span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.pvtab {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.sp {
  flex: 1;
}

/* ── 状态行 ── */
.pvtab__hd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  margin-bottom: var(--yzh-space-2, 8px);
}
.pvtab__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}

/* ── 待办清单（紧凑两列小字，⛔ 不用大块红色警示）── */
.pvtab__pend {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  padding: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-2, 8px);
  border: 1px solid var(--yzh-color-warning-light-8, #faecd8);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-warning-light-9, #fdf6ec);
  max-height: 120px;
  overflow: auto;
}
.pend {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.6;
  color: var(--yzh-color-text-regular, #606266);
}
.pend b {
  flex-shrink: 0;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  color: var(--yzh-color-warning, #d97706);
}

/* ── 可改值列表 ── */
.pvtab__rows {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}
.pvrow {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-md, 8px);
  padding: var(--yzh-space-2, 8px);
}
.pvrow__hd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-1, 4px);
}
.pvrow__ref {
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
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.pvrow__src {
  flex: 1;
  min-width: 0;
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.pvrow__dirty {
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-primary, #1e3a8a);
}

/* ── 动作行 ── */
.pvtab__ft {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  padding-top: var(--yzh-space-3, 12px);
  margin-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
  flex-shrink: 0;
}
.pvtab__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}
</style>
