/**
 * 端口字段名归一化回归测试（2026-10-11）
 *
 * 缺陷现场：nc-config 页拖「获取字段值」(get_field) 等 skill 节点到画布后，
 * inspector「输入端口 (INPUTS)」的端口标题**整片空白**，且 3 个参数全被渲染成
 * 「选择上游节点」下拉。
 *
 * 根因：后端 metadata 返回 PascalCase（`Name`/`Label`/`BindMode`/`Required`…），
 * 前端读小写（`port.name`/`port.label`/`port.bindMode`）⇒ 全 undefined。
 *
 * 本测试锁定「后端 PascalCase → 前端 PortDef 小写」这条契约，防回归。
 */
import { describe, expect, it } from 'vitest'
import { normalizePort, normalizePorts } from '@share/composables/workflow/portNormalize'

/** 真实后端返回：GET /api/Admin/Workflow/WfSkill/metadata → Code=get_field */
const GET_FIELD_PORTS = [
  {
    Name: 'field_code',
    Label: '字段编码，如 iso9001.ent_base.biz_lic.Name',
    Type: 'string',
    Required: true,
    Description: '字段编码，如 iso9001.ent_base.biz_lic.Name',
    BindMode: 'ConstantOnly'
  },
  {
    Name: 'enterprise_code',
    Label: '企业编码（空=标准样例企业=虚拟企业常量）',
    Type: 'string',
    Required: true,
    Description: '企业编码（空=标准样例企业=虚拟企业常量）',
    BindMode: 'ConstantOnly'
  },
  {
    Name: 'file_code',
    Label: '文件编码，可选，文件级过滤',
    Type: 'string',
    Required: false,
    Description: '文件编码，可选，文件级过滤',
    BindMode: 'ConstantOnly'
  }
]

describe('portNormalize', () => {
  it('把 Name/Label/Type/Required/BindMode/Description 映射为小写', () => {
    const [p] = normalizePorts(GET_FIELD_PORTS)
    expect(p.name).toBe('field_code')
    expect(p.label).toBe('字段编码，如 iso9001.ent_base.biz_lic.Name')
    expect(p.type).toBe('string')
    expect(p.required).toBe(true)
    expect(p.bindMode).toBe('ConstantOnly')
    expect(p.description).toBe('字段编码，如 iso9001.ent_base.biz_lic.Name')
  })

  it('大写键被清除 —— 不再残留 PascalCase 字段', () => {
    for (const p of normalizePorts(GET_FIELD_PORTS)) {
      for (const key of ['Name', 'Label', 'Type', 'Required', 'BindMode', 'Description', 'EnumSource', 'DefaultValue']) {
        expect(key in p).toBe(false)
      }
    }
  })

  it('★ 缺陷本体：端口标题不再空白（NodePropertyForm 读 port.label || port.name）', () => {
    const ports = normalizePorts(GET_FIELD_PORTS)
    expect(ports).toHaveLength(3)
    for (const p of ports) expect(p.label || p.name).toBeTruthy()
    // 归一化前两个都取不到 —— 这正是空白标题的成因
    for (const raw of GET_FIELD_PORTS) {
      expect((raw as any).label || (raw as any).name).toBeUndefined()
    }
  })

  it('★ bindMode 生效 —— 不再一律回落 LinkOrConstant（否则「仅常量」被误渲染成「选上游节点」）', () => {
    const ports = normalizePorts(GET_FIELD_PORTS)
    expect(ports.every((p) => p.bindMode === 'ConstantOnly')).toBe(true)
    // 若沿用 PascalCase，PortControl 只能拿到 undefined → 默认 LinkOrConstant
    expect(GET_FIELD_PORTS.every((p) => (p as any).bindMode === undefined)).toBe(true)
  })

  it('required 保留（供 REQ 标签）—— 大小写与布尔值都不丢', () => {
    expect(normalizePorts(GET_FIELD_PORTS).map((p) => p.required)).toEqual([true, true, false])
  })

  it('EnumSource 映射 —— compare 节点的「运算符」枚举下拉依赖它', () => {
    const [p] = normalizePorts([
      { Name: 'operator', Label: '运算符', Type: 'string', Required: false, BindMode: 'Enum', EnumSource: 'compare_operator' }
    ])
    expect(p.bindMode).toBe('Enum')
    expect(p.enumSource).toBe('compare_operator')
  })

  it('幂等：连续调用两次结果完全一致', () => {
    const once = normalizePorts(GET_FIELD_PORTS)
    const twice = normalizePorts(once)
    expect(JSON.stringify(twice)).toBe(JSON.stringify(once))
  })

  it('前端 specialNodes 的小写端口原样通过（不受影响）', () => {
    const native = [{ name: 'input', label: '输入数据', type: 'json', bindMode: 'LinkOrConstant', required: false }]
    expect(normalizePorts(native)).toEqual(native)
  })

  it('大小写混合时小写优先，不被大写覆盖', () => {
    const [p] = normalizePorts([{ name: 'keep_me', Name: 'overwrite_me', Label: '标签' }])
    expect(p.name).toBe('keep_me')
    expect(p.label).toBe('标签')
    expect('Name' in p).toBe(false)
  })

  it('脏输入安全：非数组返回 []，非对象原样返回', () => {
    expect(normalizePorts(undefined)).toEqual([])
    expect(normalizePorts(null)).toEqual([])
    expect(normalizePorts('nope')).toEqual([])
    expect(normalizePort(undefined)).toBeUndefined()
    expect(normalizePort(null)).toBeNull()
    expect(normalizePort('nope')).toBe('nope')
    expect(normalizePorts(['nope'])).toEqual(['nope'])
  })
})
