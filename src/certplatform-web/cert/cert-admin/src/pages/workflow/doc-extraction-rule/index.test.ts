import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'

// Element Plus stubs
const stubs = {
  'el-card': { template: '<div class="el-card"><slot /></div>', props: ['shadow'] },
  'el-collapse': { template: '<div class="el-collapse"><slot /></div>', props: ['modelValue'] },
  'el-collapse-item': { template: '<div class="el-collapse-item"><div class="el-collapse-item__header" @click="$emit(\'click\')"><slot name="title" /></div><div class="el-collapse-item__wrap\"><slot /></div></div>', props: ['name', 'title'] },
  'el-table': { template: '<div class="el-table"><slot /></div>', props: ['data', 'size', 'border', 'maxHeight'] },
  'el-table-column': { template: '<div class="el-table-column"><slot :row="{}" /></div>', props: ['label', 'prop', 'minWidth', 'width'] },
  'el-button': { template: '<button :disabled="disabled" :loading="loading" @click="$emit(\'click\')"><slot /></button>', props: ['disabled', 'loading', 'type', 'size'] },
  'el-input': { template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />', props: ['modelValue', 'placeholder', 'size'] },
  'el-select': { template: '<select :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><slot /></select>', props: ['modelValue', 'size'] },
  'el-option': { template: '<option :value="value">{{ label }}</option>', props: ['label', 'value'] },
  'el-switch': { template: '<input type="checkbox" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" />', props: ['modelValue'] },
  'el-tag': { template: '<span class="el-tag"><slot /></span>', props: ['type', 'size'] },
  'el-icon': { template: '<span class="el-icon"><slot /></span>' },
  'el-alert': { template: '<div class="el-alert"><div class="el-alert__title"><slot name="title" /></div></div>', props: ['type', 'title', 'showIcon'] },
  'el-descriptions': { template: '<div class="el-descriptions"><slot /></div>', props: ['column', 'border', 'size'] },
  'el-descriptions-item': { template: '<div class="el-descriptions-item"><span class="el-descriptions-item__label">{{ label }}</span><span class="el-descriptions-item__content"><slot /></span></div>', props: ['label'] },
  'el-empty': { template: '<div class="el-empty">{{ description }}</div>', props: ['description', 'image'] },
  'el-link': { template: '<a @click="$emit(\'click\')"><slot /></a>', props: ['type'] },
  'el-form': { template: '<div><slot /></div>', props: ['model', 'labelWidth'] },
  'el-form-item': { template: '<div><slot /></div>', props: ['label'] },
  'el-popover': { template: '<div><slot /></div>', props: ['trigger', 'width'] },
}

import AIAnalysisTab from './components/AIAnalysisTab.vue'
import PromptVerifyTab from './components/PromptVerifyTab.vue'

describe('AIAnalysisTab', () => {
  const defaultProps = {
    fields: [],
    tables: [],
    analyzing: false,
  }

  it('renders empty state correctly', () => {
    const wrapper = mount(AIAnalysisTab, {
      props: defaultProps,
      global: { stubs }
    })
    expect(wrapper.find('.ai-analysis-tab').exists()).toBe(true)
    expect(wrapper.text()).toContain('暂无字段')
    expect(wrapper.text()).toContain('暂无表格')
  })

  it('emits analyze event when button clicked', async () => {
    const wrapper = mount(AIAnalysisTab, {
      props: defaultProps,
      global: { stubs }
    })
    const btn = wrapper.find('button')
    await btn.trigger('click')
    expect(wrapper.emitted('analyze')).toBeTruthy()
  })

  it('renders fields correctly', async () => {
    const props = {
      ...defaultProps,
      fields: [
        { name: '企业名称', code: 'companyName', dataType: 'string', isRequired: true, isManual: false, isAiRecommended: true },
        { name: '地址', code: 'address', dataType: 'string', isRequired: false, isManual: true, isAiRecommended: false },
      ]
    }
    const wrapper = mount(AIAnalysisTab, { props, global: { stubs } })
    expect(wrapper.text()).toContain('字段定义（2）')
  })

  it('renders tables correctly', async () => {
    const props = {
      ...defaultProps,
      tables: [
        {
          name: '股东信息', code: 'shareholderInfo',
          columns: [{ name: '姓名', code: 'name', dataType: 'string', isRequired: true }],
          isAiRecommended: true
        }
      ]
    }
    const wrapper = mount(AIAnalysisTab, { props, global: { stubs } })
    expect(wrapper.text()).toContain('表格定义（1）')
    expect(wrapper.text()).toContain('股东信息')
  })
})

describe('PromptVerifyTab', () => {
  const defaultProps = {
    fields: [],
    tables: [],
    prompt: '',
    isValid: false,
    verifying: false,
    generating: false,
  }

  it('renders empty state correctly', () => {
    const wrapper = mount(PromptVerifyTab, {
      props: defaultProps,
      global: { stubs }
    })
    expect(wrapper.find('.prompt-verify-tab').exists()).toBe(true)
    expect(wrapper.text()).toContain('Prompt 生成与验证')
  })

  it('shows prompt content when provided', async () => {
    const props = {
      ...defaultProps,
      prompt: '请从文档中提取以下字段：企业名称、地址',
    }
    const wrapper = mount(PromptVerifyTab, { props, global: { stubs } })
    const textarea = wrapper.find('input')
    expect((textarea.element as HTMLInputElement).value).toBe('请从文档中提取以下字段：企业名称、地址')
  })

  it('emits generate event when button clicked', async () => {
    const wrapper = mount(PromptVerifyTab, {
      props: defaultProps,
      global: { stubs }
    })
    const buttons = wrapper.findAll('button')
    const generateBtn = buttons.find(b => b.text().includes('生成'))
    await generateBtn?.trigger('click')
    expect(wrapper.emitted('generate')).toBeTruthy()
  })
})
