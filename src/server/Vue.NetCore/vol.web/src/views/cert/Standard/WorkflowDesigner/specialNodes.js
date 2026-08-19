/**
 * 特殊节点清单（V1.3 前端硬编码）
 *
 * 设计原则：
 * - 特殊节点（start/end/logic/ai/loop/docField/docTable）前后端硬编码，不落 wf_skill 表
 * - 前端硬编码面板/渲染/交互，后端硬编码执行逻辑
 * - 功能节点由 GET /api/skill/catalog 返回，通用表单渲染
 *
 * 每个特殊节点定义：
 * - classCode：固定编码（与后端约定）
 * - className：显示名称
 * - category：面板分组
 * - color：节点颜色
 * - icon：面板图标
 * - singleton：是否单例（画布中只允许一个实例）
 * - maxOut：最大出边数（0=不限）
 * - testable：是否可独立测试
 * - outputStrict：输出约束强度
 * - inputPorts：输入端口定义
 * - outputPorts：输出端口定义
 * - panelSchema：属性面板字段定义（前端硬编码）
 */

export const SPECIAL_NODES = [
  {
    classCode: 'start',
    className: '开始',
    category: 'control',
    color: '#67C23A',
    icon: 'VideoPlay',
    singleton: true,
    maxOut: 0,
    testable: false,
    outputStrict: false,
    inputPorts: [],
    outputPorts: [
      { name: 'enterpriseCode', type: 'string', description: '企业编码' },
      { name: 'phaseCode', type: 'string', description: '阶段编码' },
      { name: 'fileCode', type: 'string', description: '文件编码' },
      { name: 'context', type: 'json', description: '运行上下文' }
    ],
    panelSchema: [
      {
        field: 'inputs',
        label: '输入参数',
        type: 'key-value-editor',
        description: '声明工作流输入参数（键=参数名，值=默认值/引用）'
      }
    ]
  },
  {
    classCode: 'end',
    className: '结束',
    category: 'control',
    color: '#F56C6C',
    icon: 'CircleClose',
    singleton: true,
    maxOut: 0,
    testable: false,
    outputStrict: false,
    inputPorts: [],
    outputPorts: [],
    panelSchema: [
      {
        field: 'outputConfig',
        label: '输出配置',
        type: 'output-config-editor',
        description: '每引用独立解析（ref+default），未执行分支取默认值不失败'
      }
    ]
  },
  {
    classCode: 'logic',
    className: '条件判断',
    category: 'control',
    color: '#E6A23C',
    icon: 'Switch',
    singleton: false,
    maxOut: 2,
    testable: true,
    outputStrict: true,
    inputPorts: [],
    outputPorts: [
      { name: 'result', type: 'boolean', description: '条件判断结果' },
      { name: 'success', type: 'anchor', description: '成功分支锚点' },
      { name: 'failure', type: 'anchor', description: '失败分支锚点' }
    ],
    panelSchema: [
      {
        field: 'conditions',
        label: '条件列表',
        type: 'condition-editor',
        description: 'conditions[]（8 操作符 + and/or 组合）'
      },
      {
        field: 'conditionLogic',
        label: '组合逻辑',
        type: 'select',
        options: [
          { label: 'AND（全部满足）', value: 'and' },
          { label: 'OR（任一满足）', value: 'or' }
        ],
        defaultValue: 'and'
      }
    ]
  },
  {
    classCode: 'ai',
    className: 'AI 判断',
    category: 'ai',
    color: '#9B59B6',
    icon: 'ChatDotRound',
    singleton: false,
    maxOut: 0,
    testable: true,
    outputStrict: false,
    inputPorts: [],
    outputPorts: [
      { name: 'content', type: 'string', description: 'LLM 文本输出' },
      { name: 'json', type: 'json', description: 'LLM JSON 输出' },
      { name: 'confidence', type: 'number', description: '置信度' }
    ],
    panelSchema: [
      {
        field: 'config.prompt',
        label: '提示词',
        type: 'textarea',
        required: true,
        description: '支持 {{input.xxx}} 引用，执行时渲染 + 输入数据 JSON 自动附加'
      },
      {
        field: 'config.jsonMode',
        label: 'JSON 模式',
        type: 'switch',
        defaultValue: true,
        description: '强制 LLM 输出 JSON 格式'
      },
      {
        field: 'title',
        label: '节点标题',
        type: 'input',
        required: true,
        description: '画布显示 / glossary / TRACE / AI 上下文'
      }
    ]
  },
  {
    classCode: 'loop',
    className: '循环',
    category: 'control',
    color: '#409EFF',
    icon: 'Refresh',
    singleton: false,
    maxOut: 0,
    testable: true,
    outputStrict: true,
    inputPorts: [
      { name: 'collection', type: 'json', description: '待遍历集合', required: true }
    ],
    outputPorts: [
      { name: 'results', type: 'json', description: '循环结果列表' }
    ],
    panelSchema: [
      {
        field: 'config.iterateMode',
        label: '迭代模式',
        type: 'select',
        options: [
          { label: '提示词驱动（v1）', value: 'prompt' },
          { label: '引擎 for-each（P2+）', value: 'engine' }
        ],
        defaultValue: 'prompt',
        description: 'v1 提示词驱动：collection 整包 + "逐项核验…输出数组" → LLM 一次调用'
      }
    ]
  },
  {
    classCode: 'docField',
    className: '文档字段',
    category: 'data',
    color: '#20B2AA',
    icon: 'Document',
    singleton: false,
    maxOut: 0,
    testable: true,
    outputStrict: true,
    inputPorts: [
      { name: 'fieldCode', type: 'string', description: '字段编码', required: true },
      { name: 'enterpriseCode', type: 'string', description: '企业编码', required: true }
    ],
    outputPorts: [
      { name: 'field_value', type: 'json', description: '字段值' },
      { name: 'field_name', type: 'string', description: '字段名称' },
      { name: 'confidence', type: 'number', description: '置信度' },
      { name: 'is_manual_edited', type: 'boolean', description: '是否人工编辑' }
    ],
    panelSchema: []
  },
  {
    classCode: 'docTable',
    className: '文档表格',
    category: 'data',
    color: '#20B2AA',
    icon: 'Grid',
    singleton: false,
    maxOut: 0,
    testable: true,
    outputStrict: true,
    inputPorts: [
      { name: 'tableCode', type: 'string', description: '表格编码', required: true },
      { name: 'enterpriseCode', type: 'string', description: '企业编码', required: true }
    ],
    outputPorts: [
      { name: 'rows', type: 'json', description: '表格行数据' },
      { name: 'extracted_json', type: 'json', description: '提取的 JSON' },
      { name: 'table_code', type: 'string', description: '表格编码' },
      { name: 'confidence', type: 'number', description: '置信度' }
    ],
    panelSchema: []
  }
]

/**
 * 获取特殊节点 by classCode
 */
export function getSpecialNode(classCode) {
  return SPECIAL_NODES.find(n => n.classCode === classCode)
}

/**
 * 获取所有特殊节点的 classCode 列表
 */
export const SPECIAL_NODE_CODES = SPECIAL_NODES.map(n => n.classCode)
