/**
 * YzhForm「Switch 布尔感知」组件测试 —— 企业资料参数保存报错的回归防线
 *
 * 【为什么测它】2026-10-08 用户实测 `/business/fill-param-def` 点保存报：
 *   「entity 不能为空；The JSON value could not be converted to System.Boolean.
 *     Path: $.IsRequired | LineNumber: 0 | BytePositionInLine: 369.」
 *   根因（已 curl 实测复现）：YzhForm 的 el-switch 此前写死 active-value=1/inactive-value=0，
 *   而 FillParamDef.IsRequired 是 **bool** 实体属性 —— 开关一切换就把 1/0 数字写进 formData，
 *   JSON 数字进 bool 属性 → System.Text.Json 反序列化失败 → 模型绑定失败（[FromBody] entity 为 null），
 *   两段错误文案被 Program.cs 的 Friendly() 用「；」拼成用户看到的那一条。
 *   该缺陷对所有「bool 列 + Switch 配置」的页面通用（ValidationRule.IsActive / DocFillPrompt.IsDefault 等）。
 *
 * 【判据 —— 开关值类型必须跟实体列走】
 *   - Schema.Type='boolean' 或值已是 boolean → active/inactive = true/false
 *   - int 列（IsValid，Mrz 默认 1/0）→ 保持 1/0
 *
 * 为什么放页面目录：vitest 只扫 cert-admin/src（vitest.config.ts include）。
 * 本机无 Chromium，按项目约定走 vitest 组件测试（真实 Element Plus + 真实 YzhForm）。
 */
import { describe, it, expect } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus, { ElSwitch } from 'element-plus'
import YzhForm from '@yzh-core/components/form/YzhForm.vue'
import type { YzhFormField } from '@yzh-core/components/form'

function mountForm(fields: YzhFormField[], modelValue: Record<string, any>) {
  return mount(YzhForm as any, {
    props: { fields, modelValue, showActions: false },
    global: { plugins: [ElementPlus] },
  })
}

/** bool 列（= FillParamDef.IsRequired 的等价描述） */
const boolField: YzhFormField = {
  prop: 'IsRequired',
  label: '必填',
  type: 'switch',
  fieldSchema: { Type: 'boolean', Optional: false },
}

/** int 列（= IsValid，全项目通用状态列） */
const intField: YzhFormField = {
  prop: 'IsValid',
  label: '状态',
  type: 'switch',
  fieldSchema: { Type: 'integer', Optional: false },
}

function lastUpdate(wrapper: ReturnType<typeof mountForm>): any {
  const emitted = wrapper.emitted('update:modelValue')
  expect(emitted).toBeTruthy()
  return emitted![emitted!.length - 1][0]
}

describe('YzhForm Switch 布尔感知', () => {
  it('bool 列：开关值 = true/false，值为 true 时挂载不被纠偏', async () => {
    const wrapper = mountForm([boolField], { IsRequired: true })
    const sw = wrapper.findComponent(ElSwitch)
    expect(sw.props('activeValue')).toBe(true)
    expect(sw.props('inactiveValue')).toBe(false)
    expect(sw.props('modelValue')).toBe(true)
    // 纠偏一旦发生（被改成 0/false）EP 会 emit update —— 此处必须全程无 emit
    await flushPromises()
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('bool 列：点击切换 → 向上抛 boolean（不是 1/0）', async () => {
    const wrapper = mountForm([boolField], { IsRequired: true })
    await wrapper.findComponent(ElSwitch).trigger('click')
    await flushPromises()
    const payload = lastUpdate(wrapper)
    expect(payload.IsRequired).toBe(false)
    expect(typeof payload.IsRequired).toBe('boolean')
  })

  it('int 列（IsValid）：开关值保持 1/0，点击抛数字', async () => {
    const wrapper = mountForm([intField], { IsValid: 1 })
    const sw = wrapper.findComponent(ElSwitch)
    expect(sw.props('activeValue')).toBe(1)
    expect(sw.props('inactiveValue')).toBe(0)
    await sw.trigger('click')
    await flushPromises()
    const payload = lastUpdate(wrapper)
    expect(payload.IsValid).toBe(0)
    expect(typeof payload.IsValid).toBe('number')
  })

  it('无 Schema 的手工字段：值已是 boolean → 仍走 true/false（不回归）', () => {
    const wrapper = mountForm([{ prop: 'Flag', label: '标记', type: 'switch' }], { Flag: false })
    const sw = wrapper.findComponent(ElSwitch)
    expect(sw.props('activeValue')).toBe(true)
    expect(sw.props('inactiveValue')).toBe(false)
  })

  it('bool 列历史数字值 1：挂载即纠偏为 boolean，数字绝不留在 v-model', async () => {
    const wrapper = mountForm([boolField], { IsRequired: 1 })
    await flushPromises()
    const payload = lastUpdate(wrapper)
    expect(typeof payload.IsRequired).toBe('boolean')
  })
})
