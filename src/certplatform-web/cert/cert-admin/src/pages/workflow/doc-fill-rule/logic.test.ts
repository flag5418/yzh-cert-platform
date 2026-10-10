/**
 * 标准文档填写规则 —— 页面 Logic 单测
 *
 * 【为什么这些断言值得写】
 *   本页的「完成度 / 设置状态 / 左树筛选」全部是**算出来的**（⛔ 不落库），
 *   也就是说：这些函数算错不会报错，只会让左树筛选、进度条、保存条**静默说谎**。
 *   纯函数单测是唯一能在改代码时不靠肉眼发现回归的手段。
 *
 * 【覆盖策略】
 *   - 全部离线：⛔ 不发任何 HTTP（`updateAnchor` 只测「未选中模板 ⇒ 抛错」这条早退路径）
 *   - 优先覆盖「口径唯一」的判定函数：`missingItems` 是必需项清单、测试验证通过条件、
 *     已完成判据**三处共用的那一份**，它错了三处一起错。
 */
import { describe, it, expect } from 'vitest'
import { DocFillRuleLogic, pruneTree } from './logic'

// ────────────────────────────────────────────────
// 夹具
// ────────────────────────────────────────────────

/** 造一个「文件叶子」（左树里唯一可操作的节点类型） */
function fileNode(code: string, extra: Record<string, any> = {}): any {
  return {
    Code: code,
    Name: `${code}.docx`,
    NodeType: 'file',
    IsLeaf: true,
    Extra: { kind: 'file', rawName: `${code}.docx`, ...extra },
    Children: [],
  }
}

/** 造一个导航节点（机构 / 标准 / 阶段 / 文件夹） */
function dirNode(code: string, kind: string, children: any[]): any {
  return {
    Code: code,
    Name: code,
    NodeType: kind,
    IsLeaf: children.length === 0,
    Extra: { kind },
    Children: children,
  }
}

/** 五级树：机构 → 标准 → 阶段 → 文件夹 → 文件 */
function sampleTree() {
  return [
    dirNode('O1', 'org', [
      dirNode('S1', 'standard', [
        dirNode('P1', 'stage', [
          dirNode('F1', 'folder', [fileNode('X1'), fileNode('X2')]),
          dirNode('F2', 'folder', [fileNode('X3')]),
        ]),
      ]),
    ]),
  ]
}

/** 一个「可编辑文档已配齐」的 Extra */
function doneEditableExtra(over: Record<string, any> = {}) {
  return {
    docCategory: 'editable',
    analyzeStatus: 'completed',
    typeConfirmed: true,
    hasTemplate: true,
    scanStatus: 'completed',
    orphanCount: 0,
    ...over,
  }
}

/** 一个「固定格式文档已配齐」的 Extra */
function doneFixedExtra(over: Record<string, any> = {}) {
  return {
    docCategory: 'fixed',
    analyzeStatus: 'completed',
    typeConfirmed: true,
    tagsJson: '["营业执照"]',
    docPurpose: '证明企业主体资格',
    ...over,
  }
}

/** 最小可用的 EntityConfigDto（`columns` / `formFields` 覆盖点的输入） */
const CONFIG: any = {
  EntityName: 'DocTemplateAnchor',
  Columns: [
    { FieldName: 'Id', DesName: 'Id', Type: 'TextBox', XsFlag: true, BcFlag: false, Yxk: true },
    { FieldName: 'TemplateCode', DesName: '模板', Type: 'TextBox', XsFlag: true, BcFlag: true, Yxk: false },
    { FieldName: 'AnchorRef', DesName: '锚点', Type: 'TextBox', XsFlag: true, BcFlag: true, Yxk: false },
    { FieldName: 'SourceSpec', DesName: '取值来源', Type: 'TextArea', XsFlag: true, BcFlag: true, Yxk: true },
    { FieldName: 'SourceSummary', DesName: '来源摘要', Type: 'TextBox', XsFlag: true, BcFlag: false, Yxk: true },
    { FieldName: 'AnchorType', DesName: '锚点类型', Type: 'TextBox', XsFlag: true, BcFlag: true, Yxk: false },
    { FieldName: 'HeaderKind', DesName: '页眉页脚', Type: 'TextBox', XsFlag: true, BcFlag: true, Yxk: true },
    { FieldName: 'IsValid', DesName: '启用', Type: 'Switch', XsFlag: true, BcFlag: true, Yxk: true, Mrz: '1' },
  ],
}

function newLogic(): DocFillRuleLogic {
  return new DocFillRuleLogic()
}

/**
 * `pruneTree` 的测试包装。
 *
 * 生产代码返回 `TreeNode[]`，逐层 `out[0].Children[0]…` 断言会触发
 * TS2532（`Children` 可能为 undefined）。测试里只关心形状，故收敛成一个 any 出口。
 */
function prune(nodes: any[], keep: (n: any) => boolean): any[] {
  return pruneTree(nodes, keep) as any
}

// ────────────────────────────────────────────────
// pruneTree —— 左树筛选（纯函数）
// ────────────────────────────────────────────────

describe('pruneTree', () => {
  it('无筛选（keepFile 恒真）时整棵树原样保留', () => {
    const tree = sampleTree()
    const out = prune(tree, () => true)
    expect(out).toHaveLength(1)
    expect(out[0].Children[0].Children[0].Children).toHaveLength(2)
    expect(out[0].Children[0].Children[0].Children[0].Children).toHaveLength(2)
  })

  it('只保留命中的文件叶子，其余同级文件被裁掉', () => {
    const out = prune(sampleTree(), (n) => n.Code === 'X2')
    const files = out[0].Children[0].Children[0].Children.flatMap((f: any) =>
      f.Children.map((c: any) => c.Code),
    )
    expect(files).toEqual(['X2'])
  })

  it('★ 子树被裁空的导航节点一并消失（回归：原实现条件恒真 ⇒ 树上挂一串空壳分支）', () => {
    // 只有 F2 里有文件命中 ⇒ F1 应整个消失，而不是留下一个空的「文件夹」
    const out = prune(sampleTree(), (n) => n.Code === 'X3')
    const folders = out[0].Children[0].Children[0].Children.map((f: any) => f.Code)
    expect(folders).toEqual(['F2'])
  })

  it('★ 该标准/阶段下所有文件都不命中 ⇒ 标准/阶段也一并消失', () => {
    const out = prune(sampleTree(), () => false)
    expect(out).toEqual([])
  })

  it('原本就没有子节点的导航节点保留（避免「无筛选」时视图也变）', () => {
    const tree = [dirNode('EMPTY', 'folder', [])]
    const out = prune(tree, () => false)
    expect(out.map((n: any) => n.Code)).toEqual(['EMPTY'])
  })

  it('★ 不污染源树：必须重建对象，⛔ 不能就地改 Children', () => {
    const tree = sampleTree()
    prune(tree, (n) => n.Code === 'X1')
    // 源树 F1 下仍应有 2 个文件（就地改会把它清空 ⇒ 清筛选后回不来）
    expect(tree[0].Children[0].Children[0].Children[0].Children).toHaveLength(2)
  })

  it('★ 不把未裁剪的原始 Children 带回来（回归：`{...n}` 展开导致筛选失效）', () => {
    // 若实现写成 `{ ...n }` 而不覆盖 Children，F1 会把 X1+X2 原样带回
    const out = prune(sampleTree(), (n) => n.Code === 'X3')
    const f1 = out[0].Children[0].Children[0].Children.find(
      (f: any) => f.Code === 'F1',
    )
    expect(f1).toBeUndefined()
  })
})

// ────────────────────────────────────────────────
// missingItems —— 必需项清单（三处共用的唯一口径）
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.missingItems', () => {
  it('通用两项：AI 语义分析 + 类型确认', () => {
    const logic = newLogic()
    expect(logic.missingItems({})).toEqual(['AI 语义分析', '类型确认'])
  })

  it('可编辑 + 已上传模板 + 扫描完成 + 无孤儿 ⇒ 无缺失', () => {
    const logic = newLogic()
    expect(logic.missingItems(doneEditableExtra())).toEqual([])
  })

  it('可编辑 + 已上传模板但未扫描 ⇒ 缺「锚点扫描」', () => {
    const logic = newLogic()
    expect(logic.missingItems(doneEditableExtra({ scanStatus: 'pending' }))).toEqual([
      '锚点扫描',
    ])
  })

  it('可编辑 + 扫描完成但有孤儿 ⇒ 缺「锚点来源」（且不重复报「锚点扫描」）', () => {
    const logic = newLogic()
    expect(logic.missingItems(doneEditableExtra({ orphanCount: 2 }))).toEqual([
      '锚点来源',
    ])
  })

  it('可编辑但未上传模板 ⇒ ⛔ 不要求锚点扫描（否则永远完不成）', () => {
    const logic = newLogic()
    const miss = logic.missingItems({
      docCategory: 'editable',
      analyzeStatus: 'completed',
      typeConfirmed: true,
      hasTemplate: false,
    })
    expect(miss).toEqual([])
  })

  it('★ 固定格式文档 ⛔ 不要求指纹，只要「分类标签 + 文档作用」', () => {
    const logic = newLogic()
    expect(logic.missingItems(doneFixedExtra())).toEqual([])
    expect(logic.missingItems(doneFixedExtra({ tagsJson: '[]' }))).toEqual([
      '分类标签',
    ])
    expect(logic.missingItems(doneFixedExtra({ docPurpose: '   ' }))).toEqual([
      '文档作用',
    ])
  })

  it('兼容 PascalCase（后端 Extra 两种大小写都出现过）', () => {
    const logic = newLogic()
    expect(
      logic.missingItems({
        DocCategory: 'fixed',
        AnalyzeStatus: 'completed',
        TypeConfirmed: true,
        TagsJson: '["a"]',
        DocPurpose: 'x',
      }),
    ).toEqual([])
  })
})

// ────────────────────────────────────────────────
// 完成度 / 设置状态（算出来的，不落库）
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.completion / setupStatus', () => {
  it('未选中文件 ⇒ 0/0 且状态 draft', () => {
    const logic = newLogic()
    expect(logic.anySelected).toBe(false)
    expect(logic.completion).toEqual({ done: 0, total: 0, miss: [] })
    expect(logic.setupStatus).toBe('draft')
  })

  it('可编辑文档配齐 ⇒ 3/3 且状态 done', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', doneEditableExtra())
    expect(logic.completion).toEqual({ done: 3, total: 3, miss: [] })
    expect(logic.setupStatus).toBe('done')
  })

  it('固定格式文档配齐 ⇒ 4/4 且状态 done', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', doneFixedExtra())
    expect(logic.completion).toEqual({ done: 4, total: 4, miss: [] })
    expect(logic.setupStatus).toBe('done')
  })

  it('未上传模板 ⇒ 归「未设置」（不是「正在设置」）', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', { docCategory: 'editable' })
    expect(logic.setupStatus).toBe('draft')
    // ★ 第 28 轮：分母改为「该文档实际的必配项」——
    //   未上传模板时没有锚点可谈 ⇒ 不进分母 ⇒ 2 项（AI 语义分析 + 类型确认）
    expect(logic.completion.total).toBe(2)
    expect(logic.completion.done).toBe(0)
    expect(logic.completion.miss).toEqual(['AI 语义分析', '类型确认'])
  })

  it('★ 分母与分子同源：配齐的可编辑文档 3/3、固定文档 4/4', () => {
    const a = newLogic()
    a.selectedNode = fileNode('X1', doneEditableExtra())
    expect(a.completion).toEqual({ done: 3, total: 3, miss: [] })

    const b = newLogic()
    b.selectedNode = fileNode('X1', doneFixedExtra())
    expect(b.completion).toEqual({ done: 4, total: 4, miss: [] })
  })

  it('★ 「扫描通过但没配来源」只算缺 1 项（锚点扫描 / 锚点来源共用一个槽位）', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', doneEditableExtra({ orphanCount: 3 }))
    // 分母仍是 3（⛔ 不是 4）—— 否则进度条永远差一格
    expect(logic.completion).toEqual({
      done: 2,
      total: 3,
      miss: ['锚点来源'],
    })
  })

  it('已上传模板但还有缺失 ⇒ 归「正在设置」', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', doneEditableExtra({ orphanCount: 1 }))
    expect(logic.setupStatus).toBe('setting')
  })

  it('statusOf(node) 与 setupStatus 同源', () => {
    const logic = newLogic()
    const node = fileNode('X1', doneFixedExtra())
    expect(logic.statusOf(node)).toBe('done')
  })
})

// ────────────────────────────────────────────────
// 节点属性读取（PascalCase / camelCase 双兼容 + 显示名≠业务名）
// ────────────────────────────────────────────────

describe('DocFillRuleLogic 节点属性', () => {
  it('非文件节点 ⇒ anySelected 为 false（机构/标准/阶段/文件夹不可操作）', () => {
    const logic = newLogic()
    logic.selectedNode = dirNode('O1', 'org', [])
    expect(logic.anySelected).toBe(false)
    expect(logic.nodeExtra).toEqual({})
  })

  it('★ fileName 取 Extra.rawName（左树 Name 可能拼了状态徽标，⛔ 不能当文件名用）', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', { rawName: '质量手册.docx' })
    expect(logic.fileName).toBe('质量手册.docx')
  })

  it('hasTemplate / templateCode / docCategory / isFixedDoc', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', {
      hasTemplate: true,
      templateCode: 'TPL-1',
      docCategory: 'fixed',
    })
    expect(logic.hasTemplate).toBe(true)
    expect(logic.templateCode).toBe('TPL-1')
    expect(logic.isFixedDoc).toBe(true)
  })

  it('docCategory 缺省为 editable', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1')
    expect(logic.docCategory).toBe('editable')
    expect(logic.isFixedDoc).toBe(false)
  })

  it('★ standardEditablePath 兼容 PascalCase（回归：原实现把同一表达式写了两遍）', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', {
      StandardEditablePath: 'editable/x1.doc.docx',
    })
    expect(logic.standardEditablePath).toBe('editable/x1.doc.docx')
    expect(logic.hasEditable).toBe(true)
  })
})

// ────────────────────────────────────────────────
// 树工具
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.countFiles', () => {
  it('递归统计文件叶子数', () => {
    const logic = newLogic()
    expect(logic.countFiles(sampleTree())).toBe(3)
    expect(logic.countFiles([])).toBe(0)
  })
})

describe('DocFillRuleLogic.defaultExpandedKeys', () => {
  it('默认展开到「阶段」层（机构 + 标准 + 阶段），⛔ 不全展开', () => {
    const logic = newLogic()
    logic.treeData = sampleTree()
    expect(logic.defaultExpandedKeys).toEqual(['O1', 'S1', 'P1'])
  })
})

describe('DocFillRuleLogic.breadcrumb', () => {
  it('回溯祖先路径（不含文件自身）', () => {
    const logic = newLogic()
    const tree = sampleTree()
    logic.treeData = tree
    const target = tree[0].Children[0].Children[0].Children[0].Children[0]
    logic.selectedNode = target
    expect(logic.breadcrumb).toEqual(['O1', 'S1', 'P1', 'F1'])
  })

  it('未选中 ⇒ 空数组', () => {
    const logic = newLogic()
    expect(logic.breadcrumb).toEqual([])
  })
})

// ────────────────────────────────────────────────
// 提示词引用校验
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.validatePromptAnchors', () => {
  const anchors = [{ AnchorRef: 'ENT_NAME' }, { AnchorRef: 'ENT_ADDR' }]

  it('全部存在 ⇒ 无非法引用', () => {
    const logic = newLogic()
    expect(
      logic.validatePromptAnchors(
        '企业 {{__FILL__.ENT_NAME}} 地址 {{__FILL__.ENT_ADDR}}',
        anchors,
      ),
    ).toEqual([])
  })

  it('存在不认识的锚点 ⇒ 报出，且去重', () => {
    const logic = newLogic()
    expect(
      logic.validatePromptAnchors(
        '{{__FILL__.NOPE}} 与 {{__FILL__.NOPE}}',
        anchors,
      ),
    ).toEqual(['NOPE'])
  })

  it('空提示词 ⇒ 空数组', () => {
    const logic = newLogic()
    expect(logic.validatePromptAnchors('', anchors)).toEqual([])
  })

  it('锚点清单为空 ⇒ 所有引用都是非法（这正是父页 ref 传空时的症状）', () => {
    const logic = newLogic()
    expect(logic.validatePromptAnchors('{{__FILL__.ENT_NAME}}', [])).toEqual([
      'ENT_NAME',
    ])
  })
})

// ────────────────────────────────────────────────
// ★ 第 28 轮新增：C3 类型自动判定
// ────────────────────────────────────────────────

describe('DocFillRuleLogic · C3 类型自动判定', () => {
  it('图片 / PDF ⇒ 不可解析 ⇒ 类型锁死为「固定文档」', () => {
    const logic = newLogic()
    // 库里残留 editable，也要被覆盖成 fixed
    logic.selectedNode = fileNode('X1', {
      fileType: 'pdf',
      docCategory: 'editable',
    })
    expect(logic.isParseable).toBe(false)
    expect(logic.docTypeLocked).toBe(true)
    expect(logic.effectiveDocCategory).toBe('fixed')
    expect(logic.isFixedDoc).toBe(true)
  })

  it('docx / xlsx ⇒ 可解析，类型由库里的值决定', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', {
      fileType: 'docx',
      docCategory: 'editable',
    })
    expect(logic.isParseable).toBe(true)
    expect(logic.docTypeLocked).toBe(false)
    expect(logic.effectiveDocCategory).toBe('editable')
  })

  it('★ 未知 / 空扩展名 ⇒ 按可解析放行（⛔ 不用肯定清单把文档锁死）', () => {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', { fileType: '' })
    expect(logic.isParseable).toBe(true)
    // 资料清单里实测有 1 份 txt —— 它不该被当成「不可解析」
    logic.selectedNode = fileNode('X2', { fileType: 'txt' })
    expect(logic.isParseable).toBe(true)
  })

  it('★ 不可解析 ⇒ ⛔ 不要求「类型确认」（类型由程序定，否则永远完不成）', () => {
    const logic = newLogic()
    expect(
      logic.missingItems({
        fileType: 'jpg',
        analyzeStatus: 'completed',
        tagsJson: '["资质证照"]',
        docPurpose: '证明主体资格',
      }),
    ).toEqual([])
    // 分母同样不含「类型确认」
    expect(logic.requiredItems({ fileType: 'jpg' })).toEqual([
      'AI 语义分析',
      '分类标签',
      '文档作用',
    ])
  })
})

// ────────────────────────────────────────────────
// ★ 第 28 轮新增：C8 锚点配齐闸 + C11 ai 节点识别
// ────────────────────────────────────────────────

describe('DocFillRuleLogic · C8 锚点配齐 / C11 ai 节点', () => {
  /** 造一个「已上传模板 + 扫描完成」的可编辑文档，并塞入锚点行 */
  function withAnchors(rows: any[], over: Record<string, any> = {}) {
    const logic = newLogic()
    logic.selectedNode = fileNode('X1', {
      docCategory: 'editable',
      hasTemplate: true,
      scanStatus: 'completed',
      ...over,
    })
    logic.anchorRows = rows
    return logic
  }

  const globalSpec =
    '{"combine":"firstHit","sources":[{"kind":"global","ref":"ENT_NAME"}]}'

  it('锚点全配齐 ⇒ ready', () => {
    const logic = withAnchors([
      { Code: 'A1', AnchorType: 'scalar', SourceSpec: globalSpec },
    ])
    expect(logic.anchorReadiness).toEqual({
      total: 1,
      unconfigured: 0,
      orphan: 0,
      staleRef: 0,   // ★ 目录未拉/拉失败 ⇒ 恒 0（见 logic.ts anchorReadiness 注释）
      ready: true,
    })
  })

  it('有锚点没配来源 ⇒ 不 ready，并如实报未配条数', () => {
    const logic = withAnchors([
      { Code: 'A1', AnchorType: 'scalar', SourceSpec: '{"combine":"firstHit","sources":[]}' },
      { Code: 'A2', AnchorType: 'scalar', SourceSpec: null },
    ])
    expect(logic.anchorReadiness.ready).toBe(false)
    expect(logic.anchorReadiness.unconfigured).toBe(2)
  })

  it('★ 域自动值（domain + auto）不算「未配」', () => {
    const logic = withAnchors([
      { Code: 'A1', AnchorType: 'domain', DomainKind: 'auto', SourceSpec: null },
    ])
    expect(logic.anchorReadiness.unconfigured).toBe(0)
    expect(logic.anchorReadiness.ready).toBe(true)
  })

  it('★ 孤儿锚点 ⇒ 不 ready', () => {
    const logic = withAnchors([
      { Code: 'A1', AnchorType: 'scalar', SourceSpec: globalSpec, IsOrphan: true },
    ])
    expect(logic.anchorReadiness.orphan).toBe(1)
    expect(logic.anchorReadiness.ready).toBe(false)
  })

  it('★ 没有锚点 ⇒ 不 ready（空清单不能算「已配齐」）', () => {
    expect(withAnchors([]).anchorReadiness.ready).toBe(false)
  })

  it('★ 未扫描完成 ⇒ 不 ready（即使锚点行已配齐）', () => {
    const logic = withAnchors(
      [{ Code: 'A1', AnchorType: 'scalar', SourceSpec: globalSpec }],
      { scanStatus: 'pending' },
    )
    expect(logic.anchorReadiness.ready).toBe(false)
  })

  it('★ C11：来源链里有 ai 节点才为真', () => {
    const no = withAnchors([{ Code: 'A1', AnchorType: 'scalar', SourceSpec: globalSpec }])
    expect(no.hasAiNode).toBe(false)

    const yes = withAnchors([
      {
        Code: 'A1',
        AnchorType: 'scalar',
        SourceSpec: '{"combine":"firstHit","sources":[{"kind":"ai","ref":"P1"}]}',
      },
    ])
    expect(yes.hasAiNode).toBe(true)
  })

  it('★ 未选中模板时 reloadAnchors 清空且不发请求', async () => {
    const logic = newLogic()
    logic.anchorRows = [{ Code: 'STALE' }]
    await logic.reloadAnchors()
    expect(logic.anchorRows).toEqual([])
  })
})

// ────────────────────────────────────────────────
// columns / formFields 覆盖点
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.columns', () => {
  it('隐藏 Id / TemplateCode（归属由左树决定）与恒空的 SourceSummary', () => {
    const logic = newLogic()
    logic.config.value = CONFIG
    const props = logic.columns.map((c: any) => c.prop)
    expect(props.length).toBeGreaterThan(0)
    expect(props).not.toContain('Id')
    expect(props).not.toContain('TemplateCode')
    expect(props).not.toContain('SourceSummary')
    // 保留 SourceSpec —— 来源摘要由前端按 SourceSpec 现算
    expect(props).toContain('SourceSpec')
  })

  it('IsValid 列带启用/禁用 tagMap', () => {
    const logic = newLogic()
    logic.config.value = CONFIG
    const col: any = logic.columns.find((c: any) => c.prop === 'IsValid')
    expect(col?.tagMap).toEqual({ 1: '启用', 0: '禁用' })
    expect(col?.tagTypeMap).toEqual({ 1: 'success', 0: 'info' })
  })
})

describe('DocFillRuleLogic.formFields', () => {
  it('受控值列改为下拉；IsValid 改为带 active/inactive value 的开关', () => {
    const logic = newLogic()
    logic.config.value = CONFIG
    const byProp = new Map<string, any>(
      logic.formFields.map((f: any) => [f.prop, f]),
    )

    expect(byProp.has('TemplateCode')).toBe(false) // 由左树注入
    expect(byProp.get('AnchorType')?.type).toBe('select')
    expect(byProp.get('AnchorType')?.options.map((o: any) => o.value)).toEqual([
      'scalar',
      'block',
      'table',
      'table_total',
      'domain',
    ])
    expect(byProp.get('IsValid')?.type).toBe('switch')
    expect(byProp.get('IsValid')?.fieldProps).toEqual({
      'active-value': 1,
      'inactive-value': 0,
    })
    // 页眉页脚必须可清空（空串 = 不适用）
    expect(byProp.get('HeaderKind')?.clearable).toBe(true)
    expect(byProp.get('HeaderKind')?.options[0]).toEqual({
      label: '（不适用）',
      value: '',
    })
  })
})

// ────────────────────────────────────────────────
// updateAnchor —— 回归：此前「只被调用、从未被定义」
// ────────────────────────────────────────────────

describe('DocFillRuleLogic.updateAnchor', () => {
  it('★ 方法存在（回归：AnchorRuleTab 的「必填项」开关曾因它不存在而必抛 TypeError）', () => {
    const logic = newLogic()
    expect(typeof logic.updateAnchor).toBe('function')
  })

  it('未选中模板 ⇒ 明确抛错，⛔ 不发请求', async () => {
    const logic = newLogic()
    await expect(logic.updateAnchor({ Code: 'A1' })).rejects.toThrow(
      '未选中模板',
    )
  })
})
