/**
 * 企业资料审核 · 右表树形构建（**纯函数，零运行时依赖**）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户需求：右表**也要树表** —— 「应该显示每个文档的层次结构」
 *
 * 右表一行 = 一份标准文档，而它在标准目录里是**挂在文件夹下**的
 * （`cert_standard_directory_folder`，实测 1~3 层）。所以把平铺清单按
 * `NormalizeTaskFileRow.FolderPath` 折成一棵树。
 *
 * ⛔ 层次**不由前端二次请求目录接口** —— 后端已在 `task-files` 每行带了
 *   `FolderPath`（人话路径，`/` 分隔），前端只做字符串拆段。一次请求，
 *   且「后端怎么排、前端就怎么摆」不会两边漂移。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么单独一个文件、而不是塞在 `logic.ts` 里
 *
 * `logic.ts` 顶部 import 了 `vue`（`ref`/`computed`）与 `element-plus`
 * （`ElMessage`）⇒ **在 node 里根本 import 不动**，这段逻辑就成了「只能靠肉眼
 * 看、跑不了断言」的黑盒。而 cert-auditor 这个 workspace **没有 vitest**
 * （只有 cert-admin 有）⇒ 一旦它出问题，唯一的验证手段是打开浏览器点。
 *
 * 抽成零依赖纯模块后，可以直接用 esbuild 打成 node 包喂真数据跑断言
 * （`buildFileTree` 是纯函数，输入确定则输出确定）。
 * ⚠️ 因此本文件**永远不准 import vue / element-plus / @share 的运行期成员** ——
 *   只允许 `import type`（编译期擦除，不产生运行期依赖）。
 */

import type { NormalizeTaskFileRow } from '@share/api/ent/enterprise-normalize'

/**
 * 右表树节点。
 *
 * ⚠️ 树节点**必须有自己的 `Code`**：`YzhTable` 的 `row-key` 要它，而文件夹行
 *   根本没有 `StandardFileCode`。前缀区分（`F:` 文件夹 / `S:` 文档）避免撞 key，
 *   文档侧还额外带**行序号**（同一份文档可在多个批次里各出现一次）——
 *   `row-key` 重复的后果见 `buildFileTree` 里的注释，是**静默**的。
 *
 * ⚠️⚠️ 子级字段必须叫 **`Children`（PascalCase）**，⛔ 不是 `children`：
 *   平台树契约（`TreeNode`）一律 PascalCase，前端守卫 **R2** 会拦小写读取
 *   （`node.children` / `n.children` 直接判违规）。左树的 `ReviewTreeNode`
 *   也是 `Children` —— 两个类型不统一，读代码的人迟早踩空。
 *   ⇒ 表格侧必须配套传 `:tree-props="{ children: 'Children' }"`（`el-table` 默认
 *     认 `children`，漏改这一处**树就展不开，且不报任何错**）。
 */
export interface ReviewFileTreeNode extends Partial<NormalizeTaskFileRow> {
  /** 树节点唯一键（`F:` 文件夹 / `S:` 文档）—— `YzhTable` 的 `row-key` 用 */
  Code: string
  /** 显示名（文件夹名 / 文档名） */
  Name: string
  /** `true` = 文件夹行（⛔ 不可审核、⛔ 不进导出清单，只为分层） */
  IsFolder: boolean
  /** 文件夹行：其下（含所有子孙文件夹）的文档份数 */
  Count?: number
  /** 文件夹行：其下待办合计（0 ⇒ 页面不显示） */
  PendingTotal?: number
  Children?: ReviewFileTreeNode[]
}

/**
 * ★ 树里**要不要显示最顶层文件夹**。
 *
 * 实测最顶层文件夹名 = `CS河北雄安尚龙医疗科技有限公司13485体系材料` —— 它把
 * **企业名**又写了一遍，而本页左树已经是「企业 › 阶段 › 任务」⇒ 再摆一层
 * 既重复又吃掉表格宽度（左树已占 340px）。
 *
 * ⚠️ 但**后端照旧返回完整路径**（含根）。这是有意的分工：根算不算噪声是
 *   **展示**决策，翻成 `false` 就能把根显出来，⛔ 不必改后端 / 重建 / 重启。
 */
const HIDE_ROOT_FOLDER = true

/** `FolderPath` → 段数组（去空段；空串 ⇒ `[]` ⇒ 该文档是顶层叶子） */
function splitFolderPath(path?: string | null): string[] {
  return String(path ?? '')
    .split('/')
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
}

/**
 * 把平铺的文档清单按 `FolderPath` 折成树。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 两个「看着像 bug」但**都是有意**的行为，改之前先读完
 *
 * ① **空文件夹不出现**：树完全由「有文档的行」长出来，标准目录里那些本次
 *    没跑到的文件夹**不会**摆在那里当空壳。审核页要看的是「这批跑了什么」，
 *    摆一堆空文件夹只会让人以为「跑了但没结果」。
 *
 * ② **文件夹节点带 `Count`（份数）与 `PendingTotal`（待办合计）**：这是唯一的
 *    汇总信息。⛔ 刻意**不做**「完成率取平均」这类聚合 —— 一份 0% 和一份 100%
 *    平均成 50% 是假信息，会让人以为「这半个文件夹好了」。
 *    要看结果请展开看具体文档行。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ key 用**完整路径**（含根），显示用**可见路径**（可能去掉根）
 *
 * 若 key 用可见路径，`HIDE_ROOT_FOLDER = true` 时两个不同根下的同名子文件夹
 * （如两个标准都有 `4记录文件`）会**合并成一个节点**，文档串味且无法察觉。
 * ⇒ key 恒用完整路径，显示再裁 —— 两者分开，⛔ 不要图省事合并。
 */
export function buildFileTree(rows: NormalizeTaskFileRow[]): ReviewFileTreeNode[] {
  const roots: ReviewFileTreeNode[] = []
  const folderIndex = new Map<string, ReviewFileTreeNode>()

  rows.forEach((row, index) => {
    const full = splitFolderPath(row.FolderPath)
    const visible = HIDE_ROOT_FOLDER ? full.slice(1) : full

    // 沿可见路径逐段建/取文件夹节点（key 仍用完整路径，见函数头）
    let bucket = roots
    for (let i = 0; i < visible.length; i++) {
      const key = `F:${full.slice(0, i + 1).join('/')}`
      let folder = folderIndex.get(key)
      if (!folder) {
        folder = { Code: key, Name: visible[i], IsFolder: true, Children: [] }
        folderIndex.set(key, folder)
        bucket.push(folder)
      }
      bucket = folder.Children!
    }

    bucket.push({
      ...row,
      // ⚠️ 文档 key **必须带序号**（⛔ 不能只用 `S:${StandardFileCode}`）：
      //   `row-key` 撞了的后果是 el-table 的树**静默错乱** —— 两行共用展开状态、
      //   一行展开另一行跟着动，而控制台什么都不报。
      //   同一份文档**确实**会在一个范围里出现两次：阶段级会聚合多个批次，
      //   每个批次都跑过同一份文档（实测两批次各一行、`StandardFileCode` 相同）。
      //   页面在 `loadFilesForQueues` 已按 `StandardFileCode` 去重，但那属于
      //   **调用方的语义**（「取最近一次结果」）；本函数只保证「一行一个节点、
      //   key 恒唯一」，两者职责分开。
      Code: `S:${index}:${row.StandardFileCode}`,
      Name: row.FileName,
      IsFolder: false,
    })
  })

  rollup(roots)
  sortTree(roots)
  return roots
}

/** 自底向上累加份数与待办合计（⛔ 只算「份数」与「待办」两个可加量，见函数头 ②） */
function rollup(nodes: ReviewFileTreeNode[]): { count: number; pending: number } {
  let count = 0
  let pending = 0
  for (const item of nodes) {
    if (item.IsFolder) {
      const sub = rollup(item.Children ?? [])
      item.Count = sub.count
      item.PendingTotal = sub.pending
      count += sub.count
      pending += sub.pending
    } else {
      count += 1
      pending += item.PendingCount ?? 0
    }
  }
  return { count, pending }
}

/**
 * 排序：文件夹在前、文档在后，各自按名称。
 *
 * ★ 文档组内**沿用原口径「有产物的排前面」**（10-11 之前的平铺排序就是这么定的：
 *   「⛔ 不让失败行把成功行挤下去」）—— 换个视图不该悄悄把这条改掉。
 *   同组内再按名称，保证两次刷新顺序稳定。
 */
function sortTree(nodes: ReviewFileTreeNode[]): void {
  nodes.sort((a, b) => {
    if (a.IsFolder !== b.IsFolder) return a.IsFolder ? -1 : 1
    if (!a.IsFolder && !b.IsFolder) {
      const af = a.OutputPath ? 0 : 1
      const bf = b.OutputPath ? 0 : 1
      if (af !== bf) return af - bf
    }
    return String(a.Name ?? '').localeCompare(String(b.Name ?? ''), 'zh-CN')
  })
  for (const item of nodes) {
    if (item.IsFolder && item.Children?.length) sortTree(item.Children)
  }
}
