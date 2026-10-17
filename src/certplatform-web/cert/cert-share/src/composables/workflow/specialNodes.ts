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
 *
 * ★★ 2026-10-15 —— `description` 与 `panelSchema[].description` 的写法约定（⛔ 新增节点必须遵守）
 *
 * 「节点属性」面板顶部的「!」悬浮读的就是本文件的 `description`；每个参数下方那行常驻小字
 * 读的是 `panelSchema[].description`。两者此前**没人写全**，用户在配置时无从下手。
 * 现约定统一按下面的骨架写，不要写成一两句泛泛的话：
 *
 *   description（节点级，回答「这个节点是干什么的 / 怎么接 / 有什么坑」）：
 *     【作用】一句话说清产出什么。
 *     【怎么配】按面板从上到下的顺序，逐段说清每个参数该选什么 / 该连谁。
 *     【坑】只写「不写就会踩」的：⛔ 静默失效、容易配错、语义反直觉的地方。
 *
 *   panelSchema[].description（参数级，回答「这一个参数怎么填」）：
 *     **一句话**（尽量 ≤ 30 字）—— 填什么、从哪来、留空会怎样。
 *     ★ 2026-10-16 收紧：⛔ 不要重复 label；⛔ **不要复述节点级说明** —— 用户已经能在面板
 *     顶部的「!」读到节点说明，参数行再抄一遍就是噪音（用户实测反馈「有点啰嗦」）；
 *     ⛔ 界面文案不写 ⛔ / ★ / ** / 「」 这类标注符号（那是代码注释的语法，不是给人读的文案）。
 *     ✅ 反例：「配置期选「标准文档」用样例验证；运行期引擎自动切「企业文档」，⛔ 不用改这一项。」
 *     ✅ 正例：「配置期用标准文档验证，运行期自动切企业文档。」——同一件事，一半的字数。
 *
 * ⚠️ 功能节点（后端 Skill）的同类说明**不写在这里**，而是写在 C# 的
 * `[Skill(Description=…)]` / `[SkillParam(Description=…)]` 上（代码即数据源，
 * 由 `GET /api/Admin/Workflow/WfSkill/metadata` 反射带出）。
 * 见 `WfSkillController.ResolveDescription`。
 */

/**
 * ★ 端口字段一律 **PascalCase**（2026-10-16 P3 跨边界命名统一）：
 *   `后端 SkillPortDto` = `rule_json` 端口 = 前端 `PortDef` 三处**逐字一致**，
 *   ⛔ 不再有小写字段 + `portNormalize` 归一化层（已删除，见守卫 G3）。
 */
export interface PortDef {
  Name: string
  Label: string
  Type: string
  Description: string
  DefaultValue?: any
  BindMode?: 'Link' | 'LinkOrConstant' | 'Enum'
  Required?: boolean
  /**
   * ★ 该参数是否允许为空（2026-10-12 后端新增，`Required == !allowEmpty`，两者同源）。
   * <para>后端 `[SkillParam(AllowEmpty = …)]` 三态的投影：`false` = 节点开发者显式声明
   * 「本参数不允许为空」⇒ 上游给空值时该节点以 `PRECONDITION_EMPTY` 失败。</para>
   * <para>与 `Required` 冗余但语义更直白，供面板做「空值拦截」提示；当前无消费方。</para>
   */
  AllowEmpty?: boolean
  EnumSource?: string
  MaxIn?: number
  Role?: 'anchor'
  Display?: 'visible' | 'hidden'
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
    description:
      '【作用】工作流起点，本身不执行任何逻辑。\n' +
      '【怎么配】不需要配置 —— 企业 / 标准 / 阶段等上下文由运行时引擎自动注入，画布上不需要也不允许手填。\n' +
      '【坑】⛔ 一个工作流有且只有一个起点；⛔ 机构编码不在画布上暴露，它由「规则 → 企业 → 登录人 → 空（平台标准级）」自动推导。',
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
    description:
      '【作用】工作流终点，把上游路径的执行结果收口成最终输出。\n' +
      '【怎么配】把要作为结果的节点连到「汇聚结果」即可，可以连多个。\n' +
      '【坑】⛔ 引擎对「多条线汇聚到一个节点」有**跨路径复用**限制 —— 汇聚点只看得见第一条路径的数据；' +
      '要收齐多分支结果请用「自动登记」类机制（如 stamp_judge_level 的判定清单），⛔ 不要指望 end 节点汇总。',
    inputPorts: [
      {
        Name: 'result',
        Label: '汇聚结果',
        Type: 'json',
        Description: '上游输出的总结果；多分支下只收到第一条路径（引擎限制，非配置问题）',
        BindMode: 'Link',
        Required: false,
        MaxIn: 999
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
    description:
      '【作用】按上游 bool 结果分流：为真走「条件为真」分支，为假走「条件为假」分支。\n' +
      '【怎么配】① 把产出 bool 的节点（compare / is_empty / is_field_empty / is_std_file_missing 等）连到「条件值」；' +
      '② 在下方分别选「成功分支目标」与「失败分支目标」。\n' +
      '【坑】⛔ 它只分流、不做比较 —— 比较必须由上游 compare 之类的节点完成；' +
      '⛔ 未选中的那一支在运行期标记为「跳过」（正常业务行为，不是失败）。',
    inputPorts: [
      {
        Name: 'condition',
        Label: '条件值',
        Type: 'boolean',
        Description: '上游节点（通常为 compare）输出的 bool 结果',
        BindMode: 'Link',
        Required: true
      }
    ],
    outputPorts: [
      {
        Name: 'success',
        Label: '条件为真',
        Type: 'signal',
        Description: '条件满足时执行此分支',
        Role: 'anchor'
      },
      {
        Name: 'failure',
        Label: '条件为假',
        Type: 'signal',
        Description: '条件不满足时执行此分支',
        Role: 'anchor'
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
    description:
      '【作用】调用大模型做智能判断 / 分析 / 提取，结果按「输出类型」转换后传给下游。\n' +
      '【怎么配】三段式，顺序不要换：\n' +
      '　① 参数 —— 每一项 = 一个 {{参数名}} 占位符，同时是一条依赖边（系统据此先跑来源节点）；\n' +
      '　② 提示词模板 —— 用 {{p1}} 引用参数，或点「+ 引用」插入画布节点输出；\n' +
      '　③ 输出类型 —— LLM 输出按此类型转换；选 JSON 时才会出现 Schema 校验框。\n' +
      '【坑】⛔ 参数名用短 ASCII 标识符（p1 / p2），中文说明放「标签」列 —— 中文参数名会拉高模型映射错误率；' +
      '⛔ 提示词里没被 {{}} 引用的参数等于白配；⛔ AI 节点「总有输出」，⛔ 别把它当判空的证据来源。',
    inputPorts: [
      {
        Name: 'input',
        Label: '输入数据',
        Type: 'json',
        Description: '上游节点输出（可选），可在提示词中通过 {{参数名}} 引用',
        BindMode: 'LinkOrConstant',
        Required: false
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
        field: 'config.Params',
        label: '参数',
        type: 'ai-params',
        description:
          '每项 = 提示词里的一个 {{参数名}} + 一条依赖边。名称填短 ASCII（p1/p2），中文说明填标签；' +
          '未被提示词引用的参数不生效。'
      },
      {
        field: 'config.PromptTemplate',
        label: '提示词模板',
        type: 'promptWithRef',
        required: true,
        rows: 6,
        description:
          '判定 / 提取的指令正文。用 {{p1}} 引用登记的参数，或点 + 引用插入节点输出；' +
          '引用名写错不报错，只会把占位符原文送给模型。'
      },
      {
        field: 'config.OutputType',
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
        description:
          'LLM 输出按此类型转换；是/否可直连条件分支。模型返回非法 JSON 时本节点会失败。'
      },
      // ★ 第 5 批（outputType=json + Schema 校验）：仅当输出类型为 JSON 时出现。
      //   填「字段名:类型」逐行，类型认 string/number/boolean/any + [] 数组 + a|b 枚举 + ? 可选。
      //   例：verdict:pass|fail|unknown / confidence:number / quotes:string[]
      //   ⛔ 留空 = 不校验（只解析 JSON）。配了则解析后逐字段机器校验，不符即节点失败。
      {
        field: 'config.OutputSchema',
        label: '输出 Schema（可选）',
        type: 'textarea',
        rows: 4,
        visibleWhen: { field: 'config.OutputType', equals: 'json' },
        description:
          '每行「字段:类型」，如 verdict:pass|fail|unknown；支持 [] 数组、a|b 枚举、? 可选。' +
          '留空 = 只解析不校验。'
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
    description:
      '【作用】定义一个固定值，供下游通过 {{节点标题.result}} 引用。\n' +
      '【怎么配】在「常量值」里填内容（文本 / 数字 / JSON 均可）；' +
      '引用名用的是**节点标题** —— 建议先把标题改成有意义的中文（如「阈值2」）。\n' +
      '【坑】⛔ 引用的是节点标题不是节点 ID；⛔ 改标题会让已有引用失效，记得同步改提示词。',
    inputPorts: [],
    outputPorts: [
      { Name: 'result', Label: '值', Type: 'string', Description: '配置的常量值', Display: 'visible' }
    ],
    panelSchema: [
      {
        field: 'config.Value',
        label: '常量值',
        type: 'textarea',
        required: true,
        rows: 2,
        defaultValue: '',
        description: '原样传给下游（文本 / 数字 / JSON 均可）；留空则下游取到空值。'
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
    description:
      '【作用】遍历上游输出的数组，对每个元素执行一次，输出结果数组。\n' +
      '【怎么配】把数组输出连到「循环集合」；「迭代模式」选「提示词驱动」（整包传给 LLM 一次调用）。\n' +
      '【坑】⛔ 上游给的不是数组时会当单元素处理；⛔ NC 判定链路基本用不到本节点 —— 逐条判定请用 stamp_judge_level 盖章，不要用循环。',
    inputPorts: [
      {
        Name: 'collection',
        Label: '循环集合',
        Type: 'json',
        Description: '上游节点输出的数组',
        BindMode: 'Link',
        Required: true
      }
    ],
    outputPorts: [
      {
        Name: 'results',
        Label: '循环结果',
        Type: 'json',
        Description: '每个元素执行结果组成的数组',
        Display: 'visible'
      }
    ],
    panelSchema: [
      {
        field: 'config.IterateMode',
        label: '迭代模式',
        type: 'select',
        options: [
          { label: '提示词驱动', value: 'prompt' },
          { label: '引擎 for-each', value: 'engine' }
        ],
        defaultValue: 'prompt',
        description: '提示词驱动：整包一次调用（默认）；引擎 for-each：逐元素执行（次数 = 元素个数）。'
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
    description:
      '【作用】取某个文档里某个字段的值（配置期用标准文档验证，运行期自动换成企业文档）。\n' +
      '【怎么配】① 文档类型 —— 配置期留「标准文档」；② 文档 —— 从左侧树当前标准+阶段下、已配提取规则的文档里选；' +
      '③ 字段 —— 选要取的那个字段。\n' +
      '【坑】⛔ 读的是**已提取入库**的结果 —— 目标文档没跑过提取链路，这里取到的是空；' +
      '⛔ 常被直接连到 stamp_judge_level 的「证据」—— 注意「取到空」会被判成「不可判定」而不是「通过」；' +
      '★ 与「判定」同连时证据空会优先判不可判定（盖过判定）。',
    inputPorts: [],
    outputPorts: [
      { Name: 'fieldValue', Label: '字段值', Type: 'string', Description: '提取的字段值', Display: 'visible' },
      { Name: 'confidence', Label: '置信度', Type: 'number', Description: '提取置信度', Display: 'hidden' },
      { Name: 'is_manual_edited', Label: '是否人工编辑', Type: 'boolean', Description: '该字段值是否被人工改写过（true = 人工覆盖过，报告里应标注来源）', Display: 'hidden' }
    ],
    panelSchema: [
      { field: 'config.DocType', label: '文档类型', type: 'select', options: [
        { label: '标准文档（配置验证）', value: 'standard' },
        { label: '企业文档（运行提取）', value: 'enterprise' }
      ], defaultValue: 'standard', description: '配置期用标准文档验证，运行期自动切企业文档。' },
      { field: 'config.RuleCode', label: '文档', type: 'doc-select', description: '当前标准和阶段下已配提取规则的文档；未选阶段时为空。' },
      { field: 'config.FieldCode', label: '字段', type: 'field-select', description: '所选文档已定义的字段；需先选文档。' }
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
    description:
      '【作用】取某个文档里某张表格的数据行（配置期用标准文档验证，运行期自动换成企业文档）。\n' +
      '【怎么配】① 文档类型 —— 配置期留「标准文档」；② 文档 —— 从左侧树当前标准+阶段下、已配提取规则的文档里选；' +
      '③ 表格 —— 选要取的那张表。\n' +
      '【坑】⛔ 同「文档字段」：读的是已提取入库的结果；⛔ 输出是行数组，直接连 stamp_judge_level 的「证据」时' +
      '「空表」会被判成「不可判定」—— 要判「表里该有的行有没有」请先用 is_table_empty 再取反。',
    inputPorts: [],
    outputPorts: [
      { Name: 'rows', Label: '表格数据', Type: 'json', Description: '表格行数据', Display: 'visible' },
      { Name: 'confidence', Label: '置信度', Type: 'number', Description: '提取置信度', Display: 'hidden' }
    ],
    panelSchema: [
      { field: 'config.DocType', label: '文档类型', type: 'select', options: [
        { label: '标准文档（配置验证）', value: 'standard' },
        { label: '企业文档（运行提取）', value: 'enterprise' }
      ], defaultValue: 'standard', description: '配置期用标准文档验证，运行期自动切企业文档。' },
      { field: 'config.RuleCode', label: '文档', type: 'doc-select', description: '当前标准和阶段下已配提取规则的文档；未选阶段时为空。' },
      { field: 'config.TableCode', label: '表格', type: 'table-select', description: '所选文档已定义的表格；需先选文档。' }
    ]
  },

  // ==================== 文档全文节点 ====================
  {
    classCode: 'docMarkdown',
    className: '文档全文',
    category: 'data',
    color: '#00897B',
    icon: 'Tickets',
    singleton: false,
    testable: true,
    renameable: true,
    description:
      '【作用】取某份文档**转换后的整篇 Markdown 全文**，供 AI 节点做通篇语义分析（如摘要、章节归类、整体判定）。' +
      '配置期用标准文档验证，运行期自动换成企业文档。\n' +
      '【怎么配】① 文档类型 —— 配置期留「标准文档」；② 文档 —— 从左侧树当前标准+阶段下的标准文件里选（与「标准文件缺失」同一份清单）。\n' +
      '【坑】⛔ 它吃的是**转换产物** Markdown —— 该文件还没转换完成时输出为空（不是报错）；' +
      '⛔ 全文可能很长，直接喂进 AI 提示词会占用大量 token，必要时先截断或只取关键章节；' +
      '⛔ 取不到时本节点**仍算成功**、只是输出空值 —— 要判「有没有拿到全文」请接 is_empty 再进条件分支。',
    inputPorts: [],
    outputPorts: [
      { Name: 'markdown', Label: '全文', Type: 'string', Description: '整篇文档的 Markdown 原文；未转换或文件不存在时为空', Display: 'visible' },
      { Name: 'charCount', Label: '字符数', Type: 'number', Description: '全文的字符数（0 = 未取到）', Display: 'hidden' }
    ],
    panelSchema: [
      { field: 'config.DocType', label: '文档类型', type: 'select', options: [
        { label: '标准文档（配置验证）', value: 'standard' },
        { label: '企业文档（运行提取）', value: 'enterprise' }
      ], defaultValue: 'standard', description: '配置期用标准文档验证，运行期自动切企业文档。' },
      { field: 'config.StandardFileCode', label: '文档', type: 'std-file-select', required: true, description: '当前标准和阶段下的标准文件（按文件夹分组，叶子可选）；运行期取该企业对应文件的全文。' }
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
    description:
      '【作用】判断企业是否缺少某个标准目录要求的文件，输出 result(bool)，可直接连条件分支的「条件值」。' +
      '四态 uploaded / not_required / placeholder_only / not_configured —— ★ 区分「企业没交」与「目录没配」。\n' +
      '【怎么配】只填一个「标准文件」（选项 = 左侧树当前标准+阶段下的标准文件）；' +
      '标准 / 阶段由左侧树自动带入，执行期按「当前企业 + 该文件」判定。\n' +
      '【坑】⛔ not_configured（目录没配）≠ 缺失 —— 接分支前先看清是四态里的哪一个；' +
      '⛔ 判断依据是「该企业是否上传过这份文件」，与文件内容质量无关。',
    inputPorts: [],
    outputPorts: [
      { Name: 'result', Label: '是否缺失', Type: 'boolean', Description: 'true = 缺失，可直连条件分支', Display: 'visible' },
      { Name: 'state', Label: '判定状态', Type: 'string', Description: 'uploaded / not_required / placeholder_only / not_configured', Display: 'visible' },
      { Name: 'is_required', Label: '是否必需', Type: 'boolean', Description: '该文件对本企业是否必需', Display: 'hidden' },
      { Name: 'message', Label: '说明', Type: 'string', Description: '判定结论的文字说明', Display: 'hidden' }
    ],
    // ★ 2026-10-14（用户裁定）：删掉 `config.standardCode` / `config.stageCode` 两个下拉。
    //   理由：① 左树已经定义了「标准 + 阶段」，再让用户选一次是重复负担；
    //        ② 执行期**根本不用**这两个值 —— `NodeExecutor.ExecuteStdFileMissingAsync`
    //           只读 `standardFileCode`，注释明确「刻意不传 standard_code / stage_code」
    //           （二次过滤一旦口径不符会把本来命中的行整批滤掉，静默退化成 not_configured）。
    //   现在这两个值由左树上下文自动带入（写入节点 config 仅留痕，见 NodePropertyForm），
    //   面板只保留「标准文件」一个真需要人决策的下拉。
    panelSchema: [
      { field: 'config.StandardFileCode', label: '标准文件', type: 'std-file-select', required: true, description: '当前标准和阶段下的标准文件（按文件夹分组，叶子可选）；执行期按当前企业判断是否已上传。' }
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
