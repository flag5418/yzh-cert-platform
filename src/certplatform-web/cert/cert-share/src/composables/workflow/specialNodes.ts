/**
 * 特殊节点统一元数据（V2 — 唯一数据源）
 *
 * 设计原则：
 * - 本文件是所有特殊节点定义的唯一数据源
 * - SkillPanel / NodePropertyForm / compiler.js 均从此导入
 * - 功能节点（compare/get_field 等）由后端 Skill 表注册，前端动态加载
 *
 * bindMode 三分法：
 * - Link            → 仅连线，面板渲染为下拉（选择画布上的节点）
 * - LinkOrConstant  → 可连线可输入，面板渲染为下拉 + 编辑按钮
 * - Enum            → 仅字典选择，面板渲染为下拉（调后台获取选项）
 */

export interface PortDef {
  name: string
  label: string
  type: string
  description: string
  defaultValue?: any
  bindMode?: 'Link' | 'LinkOrConstant' | 'Enum'
  required?: boolean
  /**
   * ★ 该参数是否允许为空（2026-10-12 后端新增，`Required == !allowEmpty`，两者同源）。
   * <para>后端 `[SkillParam(AllowEmpty = …)]` 三态的投影：`false` = 节点开发者显式声明
   * 「本参数不允许为空」⇒ 上游给空值时该节点以 `PRECONDITION_EMPTY` 失败。</para>
   * <para>与 `required` 冗余但语义更直白，供面板做「空值拦截」提示；当前无消费方。</para>
   */
  allowEmpty?: boolean
  maxIn?: number
  role?: 'anchor'
  display?: 'visible' | 'hidden'
}

export interface PanelField {
  field: string
  label: string
  type: string
  defaultValue?: any
  rows?: number
  min?: number
  max?: number
  step?: number
  description?: string
  enumSource?: string
  options?: Array<{ label: string; value: any }>
  required?: boolean
  /** ★ 第 5 批：条件显示 —— 当 `panelFieldValues[field]` 等于 `equals` 时才渲染该字段。
   *  例：outputSchema 只在 outputType === 'json' 时出现。不写 = 恒显示（向后兼容）。 */
  visibleWhen?: { field: string; equals: any }
}

export interface SpecialNodeMeta {
  classCode: string
  className: string
  category: 'control' | 'ai' | 'data'
  color: string
  icon: string
  singleton: boolean
  testable: boolean
  renameable: boolean
  description: string
  inputPorts: PortDef[]
  outputPorts: PortDef[]
  panelSchema: PanelField[]
}

export const SPECIAL_NODES: SpecialNodeMeta[] = [
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
    description: '调用大语言模型执行智能判断/分析，支持引用节点输出、调用 Skill 方法、常量输入，结果自动传递给下游',
    inputPorts: [
      {
        name: 'input',
        label: '输入数据',
        type: 'json',
        description: '上游节点输出（可选），可在提示词中通过 {{参数名}} 引用',
        bindMode: 'LinkOrConstant',
        required: false
      }
    ],
    outputPorts: [],
    panelSchema: [
      // ★ 2026-10-14（用户裁决 ①②④）：新增「参数」段 —— 三段式第 ① 段。
      //
      //   ① **参数即边**：这里登记的每一项（`params[].nodeId`）同时就是一条隐式依赖边，
      //      系统据此知道「必须先跑数据源节点，再跑 AI 节点」—— 孤立的 docField/docTable
      //      因此会被预执行，不再恒空（设计 §2.2 / 缺陷 D3）。
      //   ② 提示词里写 `{{p1}}`，渲染时占位符替换成参数名、值集中注入末尾「参数说明」区
      //      （注释式，用户裁决 ④）。
      //   ③ 参数名必须是短 ASCII 标识符（p1/p2），中文说明放「标签」——
      //      模型要完成「符号 → 值」映射，中文参数名会拉高弱模型的映射错误率（§2.3）。
      {
        field: 'config.params',
        label: '参数',
        type: 'ai-params',
        description: '提示词里用 {{参数名}} 引用；每一项同时是一条依赖边（自动先执行来源节点）'
      },
      {
        field: 'config.promptTemplate',
        label: '提示词模板',
        type: 'promptWithRef',
        required: true,
        rows: 6,
        description: '点击「+ 引用」插入画布节点输出，格式 {{参数名}} 或 {{节点标题.端口}}'
      },
      {
        field: 'config.outputType',
        label: '输出类型',
        type: 'select',
        required: true,
        options: [
          { label: '文本', value: 'string' },
          { label: '数字', value: 'number' },
          { label: '是/否', value: 'boolean' },
          { label: '日期', value: 'date' },
          { label: 'JSON', value: 'json' }
        ],
        defaultValue: 'string',
        description: 'LLM 输出将自动转换为此类型'
      },
      // ★ 第 5 批（outputType=json + Schema 校验）：仅当输出类型为 JSON 时出现。
      //   填「字段名:类型」逐行，类型认 string/number/boolean/any + [] 数组 + a|b 枚举 + ? 可选。
      //   例：verdict:pass|fail|unknown / confidence:number / quotes:string[]
      //   ⛔ 留空 = 不校验（只解析 JSON）。配了则解析后逐字段机器校验，不符即节点失败。
      {
        field: 'config.outputSchema',
        label: '输出 Schema（可选）',
        type: 'textarea',
        rows: 4,
        visibleWhen: { field: 'config.outputType', equals: 'json' },
        description: '每行「字段:类型」，如 verdict:pass|fail|unknown、confidence:number、quotes:string[]。留空不校验；配了则 JSON 解析后逐字段校验，不符即失败'
      }
    ]
  },

  // ==================== 常量节点 ====================
  {
    classCode: 'constant',
    className: '常量',
    category: 'data',
    color: '#909399',
    icon: 'EditPen',
    singleton: false,
    testable: false,
    renameable: true,
    description: '定义一个常量值，供 AI 节点或其他节点通过 {{常量名.result}} 引用',
    inputPorts: [],
    outputPorts: [
      { name: 'result', label: '值', type: 'string', description: '配置的常量值', display: 'visible' }
    ],
    panelSchema: [
      {
        field: 'config.value',
        label: '常量值',
        type: 'textarea',
        required: true,
        rows: 2,
        defaultValue: '',
        description: '输入常量内容（文本/数字/JSON 均可）'
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
  },

  // ==================== 标准目录文件是否缺失节点 ====================
  {
    classCode: 'is_std_file_missing',
    className: '标准文件缺失',
    category: 'data',
    color: '#D81B60',
    icon: 'FolderChecked',
    singleton: false,
    testable: true,
    renameable: true,
    description: '判断企业是否缺少某个标准目录要求的文件。配置期只需选定一个标准文件（标准/阶段取左树当前选中，⛔ 不再让用户选一次）；执行期自动带入当前企业，输出 result(bool) 可直连条件分支',
    inputPorts: [],
    outputPorts: [
      { name: 'result', label: '是否缺失', type: 'boolean', description: 'true = 缺失，可直接连条件分支的「条件值」', display: 'visible' },
      { name: 'state', label: '判定状态', type: 'string', description: 'uploaded / not_required / placeholder_only / not_configured', display: 'visible' },
      { name: 'is_required', label: '是否必需', type: 'boolean', description: '该文件对本企业是否必需', display: 'hidden' },
      { name: 'message', label: '说明', type: 'string', description: '判定结论的文字说明', display: 'hidden' }
    ],
    // ★ 2026-10-14（用户裁定）：删掉 `config.standardCode` / `config.stageCode` 两个下拉。
    //   理由：① 左树已经定义了「标准 + 阶段」，再让用户选一次是重复负担；
    //        ② 执行期**根本不用**这两个值 —— `NodeExecutor.ExecuteStdFileMissingAsync`
    //           只读 `standardFileCode`，注释明确「刻意不传 standard_code / stage_code」
    //           （二次过滤一旦口径不符会把本来命中的行整批滤掉，静默退化成 not_configured）。
    //   现在这两个值由左树上下文自动带入（写入节点 config 仅留痕，见 NodePropertyForm），
    //   面板只保留「标准文件」一个真需要人决策的下拉。
    panelSchema: [
      { field: 'config.standardFileCode', label: '标准文件', type: 'std-file-select', required: true, description: '选项 = 左树当前「标准 + 阶段」下的标准文件；执行期按「当前企业 + 该文件」判断是否已上传' }
    ]
  }
]

/**
 * 获取特殊节点 by classCode
 */
export function getSpecialNode(classCode: string): SpecialNodeMeta | undefined {
  return SPECIAL_NODES.find(n => n.classCode === classCode)
}

/**
 * 获取所有特殊节点的 classCode 列表
 */
export const SPECIAL_NODE_CODES: string[] = SPECIAL_NODES.map(n => n.classCode)

/**
 * 获取特殊节点的配色（供 compiler.js 使用）
 */
export function getSpecialNodeStyle(classCode: string): { fill: string; stroke: string; strokeWidth: number } | null {
  const node = getSpecialNode(classCode)
  if (!node) return null
  return {
    fill: lightenColor(node.color, 0.85),
    stroke: node.color,
    strokeWidth: 2
  }
}

/** 简单颜色加亮（hex -> rgba 混合白色） */
function lightenColor(hex: string, ratio: number): string {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  const lr = Math.round(r + (255 - r) * ratio)
  const lg = Math.round(g + (255 - g) * ratio)
  const lb = Math.round(b + (255 - b) * ratio)
  return `#${lr.toString(16).padStart(2, '0')}${lg.toString(16).padStart(2, '0')}${lb.toString(16).padStart(2, '0')}`
}
