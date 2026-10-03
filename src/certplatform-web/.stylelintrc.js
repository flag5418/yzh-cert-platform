/**
 * 样式门禁 —— docs/10-YZH架构/25-样式规范与硬编码治理-V1.md §五 P0
 *
 * 分工（避免与 guards R18 重复报警）：
 *   · 颜色 / 字号 / 间距 硬编码 → 交给 guards.mjs **R18**（style-baseline.json 基线，只准减不准增）
 *     ⛔ 故意不开 color-no-hex：stylelint 会把 `var(--yzh-color-danger, #f56c6c)` 的兜底值也算违规，
 *        而「令牌 + 兜底」正是本项目要求的写法（25 号 §四）。
 *     ⛔ 故意不开 declaration-no-important：本项目仅允许在 `:deep()` 覆盖 Element Plus 时用 !important，
 *        见 25 号 §四「!important 白名单」。
 *   · 结构性 CSS 错误 → 交给本文件（规则须保持现状 0 违规，新增即红）
 *
 * 跑法：`npm run lint:style`（已并入 `npm run guard`）
 */
module.exports = {
  extends: ['stylelint-config-recommended-vue'],
  ignoreFiles: ['**/dist/**', '**/node_modules/**', '**/coverage/**', '**/*.min.css'],
  rules: {
    'selector-class-pattern': [
      '^[a-z][a-z0-9-]*(__[a-z0-9-]+)?(--[a-z0-9-]+)?$|^is-[a-z0-9-]+$|^el(-[a-z0-9-]+)*$',
      { resolveNestedSelectors: true },
    ],
    'no-duplicate-selectors': true,
    'no-descending-specificity': true,
    'unit-no-unknown': true,
    'function-no-unknown': [true, { ignoreFunctions: ['v-bind', 'var'] }],
    'declaration-property-value-keyword-no-deprecated': true,
  },
}
