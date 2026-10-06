/**
 * 全文填写提示词的 **`PromptCode` 生成器**（纯函数，可单测）。
 *
 * 【为什么单独一个模块】
 *   设计 `41` §8.5.4 裁定「`PromptCode` 自动生成」，而这一列是**主键**：
 *   生成规则写错不会有编译错误，只会在**运行期**表现为「新建按钮永远失败」
 *   （后端 500 / 校验拒绝）。把它抽成纯函数并配单测，改坏了立刻红。
 *
 * 【★ 后端约束（逐字对齐 `DocFillPromptController`）】
 *   ① `add` 校验 `string.IsNullOrWhiteSpace(p.PromptCode)` ⇒ **不接受空编码**，
 *      而 `add` 由 CRUD 基类提供、没有「代生成」钩子 ⇒ 生成这一步只能落在前端。
 *   ② 格式 `^[a-z][a-z0-9_]{1,49}$`（小写字母开头，只含小写字母 / 数字 / 下划线，长 2~50）。
 *
 * 【为什么是「时间戳 + 随机后缀」而不是「模板名转拼音」】
 *   模板名是中文（如「质量手册」）—— 转拼音要引依赖，且不同模板极易撞码。
 *   编码在本页只是**主键**，可读性由 `PromptName` 承担（版本表与下拉都显示名称）。
 */

/** 固定前缀：`dfp` = **d**oc **f**ill **p**rompt，便于在「提示词工作台」里一眼认出机器生成的码 */
export const PROMPT_CODE_PREFIX = 'dfp_'

/** 与后端逐字一致 */
export const PROMPT_CODE_PATTERN = /^[a-z][a-z0-9_]{1,49}$/

/** 该字符串是否是后端接受的 `PromptCode` 形态 */
export function isPromptCode(v: string): boolean {
  return PROMPT_CODE_PATTERN.test(v)
}

/**
 * 生成一个 `PromptCode`。
 *
 * @param now  base36 时间戳源（默认 `Date.now()`，测试可注入）
 * @param rand `[0,1)` 随机数（默认 `Math.random()`，测试可注入）
 *
 * 结构：`dfp_` + `now.toString(36)` + 4 位随机 base36 片段 ⇒ 当前长度 16，恒满足格式。
 */
export function genPromptCode(
  now: number = Date.now(),
  rand: number = Math.random(),
): string {
  const ts = now.toString(36)
  // 随机片段恒补足 4 位：`Math.random()` 有可能极小（`toString(36)` 后只剩 1~2 位）
  const suffix = rand.toString(36).slice(2, 6).padEnd(4, '0')
  return `${PROMPT_CODE_PREFIX}${ts}${suffix}`
}
