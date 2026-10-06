/**
 * `PromptCode` 生成器 —— 纯函数测试
 *
 * 【为什么值得测】
 *   生成规则写错**没有编译错误**，只会让「新建提示词」按钮**每次都失败**
 *   （后端 `add` 校验 `PromptCode` 非空 + 格式 `^[a-z][a-z0-9_]{1,49}$`）。
 *   本文件把「生成物必须被后端接受」这条契约钉死。
 */
import { describe, it, expect } from 'vitest'
import {
  genPromptCode,
  isPromptCode,
  PROMPT_CODE_PATTERN,
  PROMPT_CODE_PREFIX,
} from './promptCode'

describe('isPromptCode — 与后端逐字一致的格式判据', () => {
  it('接受：小写字母开头，只含小写字母 / 数字 / 下划线，长 2~50', () => {
    expect(isPromptCode('ab')).toBe(true)
    expect(isPromptCode('dfp_m3k9x2ab7f')).toBe(true)
    expect(isPromptCode('a_1_b')).toBe(true)
    // 恰好 50 位
    expect(isPromptCode('a' + 'b'.repeat(49))).toBe(true)
  })

  it('拒绝：大写 / 数字开头 / 下划线开头 / 太短 / 太长 / 非法字符', () => {
    expect(isPromptCode('Ab')).toBe(false)
    expect(isPromptCode('1ab')).toBe(false)
    expect(isPromptCode('_ab')).toBe(false)
    expect(isPromptCode('a')).toBe(false) // 长 1 < 2
    expect(isPromptCode('a' + 'b'.repeat(50))).toBe(false) // 长 51 > 50
    expect(isPromptCode('a-b')).toBe(false)
    expect(isPromptCode('a b')).toBe(false)
    expect(isPromptCode('')).toBe(false)
  })
})

describe('genPromptCode — 生成物必须满足后端契约', () => {
  it('★ 默认参数下生成 200 个，全部满足格式（这是「新建不失败」的底线）', () => {
    const codes = Array.from({ length: 200 }, () => genPromptCode())
    for (const c of codes) {
      expect(PROMPT_CODE_PATTERN.test(c)).toBe(true)
    }
    expect(new Set(codes).size).toBeGreaterThan(190) // 时间戳 + 4 位随机 ⇒ 实际几乎无碰撞
  })

  it('带 `dfp_` 前缀（便于在提示词工作台里认出机器生成的码）', () => {
    expect(genPromptCode().startsWith(PROMPT_CODE_PREFIX)).toBe(true)
  })

  it('★ 随机数极小（toString(36) 后只剩 1~2 位）也要补足，⛔ 不能产出过短的码', () => {
    // Math.random() 返回 0 是最坏情况：'0'.toString(36).slice(2,6) === ''
    const c = genPromptCode(1700000000000, 0)
    expect(c).toBe('dfp_' + (1700000000000).toString(36) + '0000')
    expect(PROMPT_CODE_PATTERN.test(c)).toBe(true)
  })

  it('now / rand 可注入 ⇒ 结果可复现（测试与排障需要确定性）', () => {
    expect(genPromptCode(1700000000000, 0.5)).toBe(
      genPromptCode(1700000000000, 0.5),
    )
  })

  it('时间戳推进 1ms ⇒ 码不同（同秒内连续新建不会撞主键）', () => {
    expect(genPromptCode(1700000000000, 0.5)).not.toBe(
      genPromptCode(1700000000001, 0.5),
    )
  })
})
