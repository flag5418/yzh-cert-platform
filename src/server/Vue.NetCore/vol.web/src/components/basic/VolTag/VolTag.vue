<template>
  <el-tag
    class="vol-tag"
    :class="{
      'vol-tag--solid-fill': !!accentHex && !isPresetSoft2,
      'vol-tag--soft': isPresetSoft2 && !!accentHex,
    }"
    :style="tagSoftStyle"
    :type="tagType"
    :color="isPresetSoft2 ? undefined : accentHex"
    :size="size"
    :effect="tagEffect"
    :closable="closable"
    :round="round"
    :hit="hit"
    :disable-transitions="disableTransitions"
    v-bind="attrs"
    @close="emit('close', $event)"
    @click="emit('click', $event)"
  >
    <slot>
      <span v-if="preset2DefaultLabel">{{ preset2DefaultLabel }}</span>
    </slot>
  </el-tag>
</template>

<script setup>
import { computed, useAttrs } from "vue";
import {
  VOL_TAG_EP_TYPES as EP_TYPES,
  VOL_TAG_PRESET_HEX as PRESET_HEX,
} from "./volTagColors.js";

defineOptions({ inheritAttrs: false });

const props = defineProps({
  /**
   * 实色 / EP 内置 / 预设名 / # 与 rgb
   * 浅色：传预设的 `{名}2`（如 blue2），非 EP 的 primary2 等
   */
  type: { type: String, default: "" },
  size: { type: String, default: undefined },
  /** 未传时：实心底 dark；预设 xxx2 浅色为 light；EP 无 accent 为 light */
  effect: { type: String, default: undefined },
  closable: { type: Boolean, default: false },
  round: { type: Boolean, default: false },
  hit: { type: Boolean, default: false },
  disableTransitions: { type: Boolean, default: false },
});

const emit = defineEmits(["close", "click"]);
const attrs = useAttrs();

const raw = computed(() => (props.type || "primary").trim());

/** 以 type 结尾的 `2` 识别浅色：仅 VOL_TAG_PRESET_HEX 中的名 + 2，不含 EP 五种 */
const preset2Match = computed(() => {
  const t = raw.value;
  if (/^#|^rgb/i.test(t)) return null;
  const m = t.match(/^(.+)(2)$/i);
  if (!m) return null;
  const base = m[1].toLowerCase();
  if (EP_TYPES.includes(base)) return null;
  if (PRESET_HEX[base]) return { base };
  return null;
});

const isPresetSoft2 = computed(() => !!preset2Match.value);

const accentHex = computed(() => {
  const p2 = preset2Match.value;
  if (p2) return PRESET_HEX[p2.base];
  const t = raw.value;
  if (/^#|^rgb/i.test(t)) return t;
  const k = t.toLowerCase();
  if (EP_TYPES.includes(k)) return undefined;
  if (PRESET_HEX[k]) return PRESET_HEX[k];
  return undefined;
});

const tagType = computed(() => {
  if (preset2Match.value) return undefined;
  const t = raw.value;
  if (/^#|^rgb/i.test(t)) return undefined;
  const k = t.toLowerCase();
  if (EP_TYPES.includes(k)) return k;
  if (PRESET_HEX[k]) return undefined;
  return "primary";
});

const tagEffect = computed(() => {
  if (props.effect !== undefined && props.effect !== null && props.effect !== "") {
    return props.effect;
  }
  if (preset2Match.value && accentHex.value) return "light";
  if (accentHex.value) return "dark";
  return "light";
});

const tagSoftStyle = computed(() =>
  preset2Match.value && accentHex.value
    ? { "--vol-tag-accent": accentHex.value }
    : {}
);

/** 无插槽且为预设 xxx2 时默认显示 blue2 形式文案 */
const preset2DefaultLabel = computed(() => {
  const p2 = preset2Match.value;
  if (!p2) return "";
  return `${p2.base}2`;
});
</script>

<style scoped>
.vol-tag {
  vertical-align: middle;
}

/*
 * 使用 el-tag 的 color（自定义 hex / 预设映射）时去掉边框。
 * EP 全局样式里 tag 带 border-width + border-style，仅写 border:none 易被权重盖住，故用 !important 并清零变量。
 */
.vol-tag.vol-tag--solid-fill.el-tag {
  border: none !important;
  border-width: 0 !important;
  --el-tag-border-color: transparent;
}

/* 预设 type 为 xxx2：类似 EP light — 淡底与边框，字略深 */
.vol-tag.vol-tag--soft.el-tag {
  --vol-tag-soft-fg: color-mix(in srgb, var(--vol-tag-accent) 78%, #0f172a);
  --vol-tag-soft-border: color-mix(in srgb, var(--vol-tag-accent) 26%, #ffffff);
  background-color: color-mix(in srgb, var(--vol-tag-accent) 8%, #ffffff) !important;
  color: var(--vol-tag-soft-fg) !important;
  border-style: solid !important;
  border-width: 1px !important;
  border-color: var(--vol-tag-soft-border) !important;
  --el-tag-border-color: var(--vol-tag-soft-border);
}
</style>
