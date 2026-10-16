/**
 * 节点说明 → el-tooltip 可用的富文本（`raw-content`）
 *
 * ── 为什么需要它 ──────────────────────────────────────────────────────
 * 节点说明（属性面板顶部的「!」、节点库的悬停提示）现在是多行结构化文本
 * （【作用】/【怎么配】/【坑】，见 `specialNodes.ts` 头部约定与后端
 * `[Skill(Description=…)]`），而 `el-tooltip` 的 `content` 是**纯文本** ——
 * 换行会被折叠成空格，多行说明挤成一坨，读不了。
 *
 * ── 做法 ─────────────────────────────────────────────────────────────
 * 转义 → 包一层「限宽 + `white-space: pre-line`」的 div，交给 `raw-content` 渲染。
 *
 * ⛔ **必须先转义**：`raw-content` 会把内容当 HTML 解析。说明文本里出现
 * `<` `>`（如 `a<b`、`<br>`、`{{节点标题.result}}` 之类）会破坏结构甚至注入。
 *
 * ⚠️ 说明是**开发者写的静态文案**（不是用户输入），但转义仍然必要 ——
 * 它防的是「写说明时手滑打了个尖括号」，不是防攻击。
 *
 * ── 用法 ─────────────────────────────────────────────────────────────
 *   <el-tooltip raw-content :content="toDescriptionHtml(node.description)" ...>
 *
 * 空 / 空白说明返回 `''` ⇒ 调用方配 `v-if` 即可隐藏提示图标。
 */

/** 单行说明的默认最大宽度（px）。太长会横跨半个屏幕，太短会让长句折得很碎。 */
const DEFAULT_MAX_WIDTH = 460

/** HTML 转义（只处理会破坏标签结构的三个字符，够用且不引入依赖） */
function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/**
 * 把说明文本转成 el-tooltip `raw-content` 可渲染的 HTML 片段。
 * @param text 说明文本（可含 `\n`；空/空白返回 `''`）
 * @param maxWidth 限宽（px），默认 460
 */
export function toDescriptionHtml(text: string | null | undefined, maxWidth: number = DEFAULT_MAX_WIDTH): string {
  const raw = (text || '').trim()
  if (!raw) return ''
  return `<div style="max-width:${maxWidth}px;white-space:pre-line;line-height:1.7">${escapeHtml(raw)}</div>`
}
