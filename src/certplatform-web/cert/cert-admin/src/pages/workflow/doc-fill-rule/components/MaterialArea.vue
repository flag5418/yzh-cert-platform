<script setup lang="ts">
/**
 * ★ 素材区 —— 提供「锚点 / 全局参数 / 系统方法」的快捷书签
 *
 * 【设计意图】
 *   在配置提示词或规则时，用户需要引用锚点或参数。
 *   横向滚动的 Chip 既省空间，又方便「点一下即复制/插入」。
 */
import { listFillParamDefs } from '@share/api/workflow/doc-fill-rule'
import { onMounted, ref } from 'vue'

defineProps<{
  /** 当前模板的锚点列表 */
  anchors: any[]
}>()

const emit = defineEmits<{
  (e: 'select', val: string): void
}>()

const params = ref<{ value: string; label: string }[]>([])
const loading = ref(false)

const methods = [
  {
    label: '日期格式',
    value: '日期一律用 YYYY-MM-DD（如 2026-03-11）。',
    hint: '日期写法约定',
  },
  {
    label: '金额单位',
    value: '金额统一为人民币元，保留 2 位小数。',
    hint: '金额写法约定',
  },
  {
    label: '序号规则',
    value: '序号从 1 起连续编号，⛔ 不留空行。',
    hint: '序号写法约定',
  },
  {
    label: '签字要求',
    value: '需签字处须手写签名并加盖部门章。',
    hint: '签字写法约定',
  },
  {
    label: '不留空',
    value: '所有单元格不得留空，不适用填「/」。',
    hint: '通用写法约定',
  },
  {
    label: '筛选口径',
    value: '本格只收企业自有的、覆盖全部过程的记录；不具备的可提交说明函。',
    hint: '筛选要求',
  },
  { label: '系统日期', value: '{{__NOW__}}', hint: '当前系统时间 token' },
  { label: '企业画像', value: '{{__PROFILE__.}}', hint: '企业资料画像 token' },
]

async function loadParams() {
  loading.value = true
  try {
    const res = await listFillParamDefs()
    params.value = (res?.data?.Items ?? []).map((p: any) => ({
      value: `{{${p.ParamCode || p.Code}}}`,
      label: p.ParamName || p.ParamCode || p.Code,
    }))
  } finally {
    loading.value = false
  }
}

onMounted(loadParams)

function onChipClick(val: string) {
  emit('select', val)
}
</script>

<template>
  <div class="material-area">
    <div class="pmrow">
      <span class="pmg"
        ><i>⚓</i>锚点引用<span class="cnt">{{ anchors.length }}</span></span
      >
      <div class="pmb">
        <button
          v-for="a in anchors"
          :key="a.AnchorRef"
          class="pmchip"
          @click="onChipClick(`{{__FILL__.${a.AnchorRef}}}`)"
        >
          {{ a.AnchorRef }}
        </button>
        <span v-if="!anchors.length" class="mat-empty">无锚点</span>
      </div>
    </div>

    <div class="pmrow">
      <span class="pmg"
        ><i>▤</i>全局参数<span class="cnt">{{ params.length }}</span></span
      >
      <div class="pmb" v-loading="loading">
        <button
          v-for="p in params"
          :key="p.value"
          class="pmchip"
          @click="onChipClick(p.value)"
        >
          {{ p.label }}
        </button>
      </div>
    </div>

    <div class="pmrow">
      <span class="pmg"
        ><i>✎</i>系统方法<span class="cnt">{{ methods.length }}</span></span
      >
      <div class="pmb">
        <button
          v-for="m in methods"
          :key="m.value"
          class="pmchip m"
          :title="m.hint"
          @click="onChipClick(m.value)"
        >
          {{ m.label }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.material-area {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.pmrow {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) 0;
  min-width: 0;
}

.pmrow + .pmrow {
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
}

.pmg {
  flex: 0 0 96px;
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
  white-space: nowrap;
}

.pmg i {
  font-style: normal;
  color: var(--yzh-color-primary, #409eff);
  font-size: var(--yzh-font-size-sm, 13px);
}

.pmg .cnt {
  font-weight: 400;
  color: var(--yzh-color-text-placeholder, #909399);
  font-size: var(--yzh-font-size-xs, 11px);
  margin-left: auto;
}

.pmb {
  flex: 1;
  min-width: 0;
  display: flex;
  gap: 5px;
  overflow-x: auto;
  padding: var(--yzh-space-1, 3px) 0;
  scrollbar-width: none;
}

.pmb::-webkit-scrollbar {
  display: none;
}

.pmchip {
  flex: 0 0 auto;
  border: 1px solid var(--yzh-color-border, #dcdfe6);
  border-radius: 11px;
  background: var(--yzh-color-bg-container, #fff);
  cursor: pointer;
  padding: var(--yzh-space-1, 3px) var(--yzh-space-2, 10px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  white-space: nowrap;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
}

.pmchip.m {
  font-family: inherit;
}

.pmchip:hover {
  border-color: var(--yzh-color-primary, #409eff);
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  color: var(--yzh-color-primary, #409eff);
}

.pmchip:active {
  transform: translateY(0);
  background: var(--yzh-color-primary-light-8, #d9ecff);
}

.mat-empty {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #c0c4cc);
  font-style: italic;
}
</style>
