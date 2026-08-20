/**
 * 特殊节点统一元数据（V2 — 唯一数据源）
 *
 * 设计原则：
 * - 本文件是所有特殊节点定义的唯一数据源
 * - SkillPanel / NodePropertyForm / compiler.js 均从此导入
 * - 功能节点（compare/get_field 等）由后端 Skill 表注册，前端动态加载
 *
 * 节点元数据模型见：
 * docs/80-功能设计/01-系统管理/工作流管理/02-NC规则配置/工作流节点定义与属性抽象-V1.md
 *
 * bindMode 三分法：
 * - Link            → 仅连线，面板渲染为下拉（选择画布上的节点）
 * - LinkOrConstant  → 可连线可输入，面板渲染为下拉 + 编辑按钮
 * - Enum            → 仅字典选择，面板渲染为下拉（调后台获取选项）
 */

export const SPECIAL_NODES = [
  // ==================== 开始节点 ====================
  {
    classCode: 'start',
    className: '开始',
    category: 'control',
    color: '#67C23A',
    icon: 'VideoPlay',
    singleton: true,
    testable: false,
    renameable: false,
    description: '工作流起点，运行时引擎自动注入企业编码、标准编码、阶段编码、文件编码等上下文参数',
    inputPorts: [],
    // 开始节点无显式输出端口面板展示（引擎内部注入）
    outputPorts: [],
    panelSchema: []
  },

  // ==================== 结束节点 ====================
  {
    classCode: 'end',
    className: '结束',
    category: 'control',
    color: '#F56C6C',
    icon: 'CircleClose',
    singleton: false,
    testable: false,
    renameable: true,
    description: '工作流终点，汇聚上游所有路径的执行结果作为最终输出',
    inputPorts: [
      {
        name: 'result',
        label: '汇聚结果',
        type: 'json',
        description: '上游节点输出结果，支持多个上游汇聚',
        bindMode: 'Link',
        required: false,
        maxIn: 999
      }
    ],
    outputPorts: [],
    panelSchema: []
  },

  // ==================== 条件分支节点 ====================
  {
    classCode: 'branch',
    className: '条件分支',
    category: 'control',
    color: '#E6A23C',
    icon: 'Switch',
    singleton: false,
    testable: false,
    renameable: true,
    description: '根据上游 bool 结果分流：条件为真走 success 路径，条件为假走 failure 路径。不含比较逻辑，比较由 compare 节点完成',
    inputPorts: [
      {
        name: 'condition',
        label: '条件值',
        type: 'boolean',
        description: '上游节点（通常为 compare）输出的 bool 结果',
        bindMode: 'Link',
        required: true
      }
    ],
    outputPorts: [
      {
        name: 'success',
        label: '条件为真',
        type: 'signal',
        description: '条件满足时执行此分支',
        role: 'anchor'
      },
      {
        name: 'failure',
        label: '条件为假',
        type: 'signal',
        description: '条件不满足时执行此分支',
        role: 'anchor'
      }
    ],
    panelSchema: []
  },

  // ==================== AI 节点 ====================
  {
    classCode: 'ai_node',
    className: 'AI 节点',
    category: 'ai',
    color: '#9C27B0',
    icon: 'ChatDotRound',
    singleton: false,
    testable: true,
    renameable: true,
    description: '调用 LLM 执行提示词，输出结果自动传递给下游节点（下游可通过 {{节点名称.content}} 引用）',
    inputPorts: [
      {
        name: 'input',
        label: '输入数据',
        type: 'json',
        description: '上游节点输出或手动输入，可在提示词中通过 {{input}} 引用',
        bindMode: 'LinkOrConstant',
        required: false
      }
    ],
    // AI 节点不展示输出端口（引擎内部有 content/json/confidence）
    outputPorts: [],
    panelSchema: [
      {
        field: 'config.prompt',
        label: '提示词',
        type: 'textarea',
        required: true,
        description: '支持 {{n1.portName}} 引用上游节点输出，执行时自动渲染'
      },
      {
        field: 'config.jsonMode',
        label: 'JSON 模式',
        type: 'switch',
        defaultValue: true,
        description: '强制 LLM 输出 JSON 格式'
      }
    ]
  },

  // ==================== 循环节点 ====================
  {
    classCode: 'loop',
    className: '循环',
    category: 'control',
    color: '#00BCD4',
    icon: 'Refresh',
    singleton: false,
    testable: true,
    renameable: true,
    description: '遍历上游输出的数组集合，对每个元素执行子流程，输出循环结果数组',
    inputPorts: [
      {
        name: 'collection',
        label: '循环集合',
        type: 'json',
        description: '上游节点输出的数组',
        bindMode: 'Link',
        required: true
      }
    ],
    outputPorts: [
      {
        name: 'results',
        label: '循环结果',
        type: 'json',
        description: '每个元素执行结果组成的数组',
        display: 'visible'
      }
    ],
    panelSchema: [
      {
        field: 'config.iterateMode',
        label: '迭代模式',
        type: 'select',
        options: [
          { label: '提示词驱动', value: 'prompt' },
          { label: '引擎 for-each', value: 'engine' }
        ],
        defaultValue: 'prompt',
        description: '提示词驱动：collection 整包传入 LLM 一次调用'
      }
    ]
  },

  // ==================== 文档字段节点 ====================
  {
    classCode: 'docField',
    className: '文档字段',
    category: 'data',
    color: '#4CAF50',
    icon: 'Document',
    singleton: false,
    testable: true,
    renameable: true,
    description: '从文档中提取指定字段的值。配置期选择标准文档验证，运行期对企业文档执行提取',
    inputPorts: [],
    outputPorts: [
      { name: 'fieldValue', label: '字段值', type: 'string', description: '提取的字段值', display: 'visible' },
      { name: 'confidence', label: '置信度', type: 'number', description: '提取置信度', display: 'hidden' },
      { name: 'is_manual_edited', label: '是否人工编辑', type: 'boolean', description: '', display: 'hidden' }
    ],
    panelSchema: [
      { field: 'config.docType', label: '文档类型', type: 'select', options: [
        { label: '标准文档（配置验证）', value: 'standard' },
        { label: '企业文档（运行提取）', value: 'enterprise' }
      ], defaultValue: 'standard', description: '标准文档用于配置期验证，企业文档用于运行期提取' },
      { field: 'config.ruleCode', label: '文档', type: 'doc-select', description: '选择已配置提取规则的文档' },
      { field: 'config.fieldCode', label: '字段', type: 'field-select', description: '选择要提取的字段' }
    ]
  },

  // ==================== 文档表格节点 ====================
  {
    classCode: 'docTable',
    className: '文档表格',
    category: 'data',
    color: '#FF9800',
    icon: 'Grid',
    singleton: false,
    testable: true,
    renameable: true,
    description: '从文档中提取指定表格的数据行。配置期选择标准文档验证，运行期对企业文档执行提取',
    inputPorts: [],
    outputPorts: [
      { name: 'rows', label: '表格数据', type: 'json', description: '表格行数据', display: 'visible' },
      { name: 'confidence', label: '置信度', type: 'number', description: '提取置信度', display: 'hidden' }
    ],
    panelSchema: [
      { field: 'config.docType', label: '文档类型', type: 'select', options: [
        { label: '标准文档（配置验证）', value: 'standard' },
        { label: '企业文档（运行提取）', value: 'enterprise' }
      ], defaultValue: 'standard', description: '标准文档用于配置期验证，企业文档用于运行期提取' },
      { field: 'config.ruleCode', label: '文档', type: 'doc-select', description: '选择已配置提取规则的文档' },
      { field: 'config.tableCode', label: '表格', type: 'table-select', description: '选择要提取的表格' }
    ]
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

/**
 * 获取特殊节点的配色（供 compiler.js 使用）
 */
export function getSpecialNodeStyle(classCode) {
  const node = getSpecialNode(classCode)
  if (!node) return null
  return {
    fill: lightenColor(node.color, 0.85),
    stroke: node.color,
    strokeWidth: 2
  }
}

/** 简单颜色加亮（hex → rgba 混合白色） */
function lightenColor(hex, ratio) {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  const lr = Math.round(r + (255 - r) * ratio)
  const lg = Math.round(g + (255 - g) * ratio)
  const lb = Math.round(b + (255 - b) * ratio)
  return `#${lr.toString(16).padStart(2, '0')}${lg.toString(16).padStart(2, '0')}${lb.toString(16).padStart(2, '0')}`
}
