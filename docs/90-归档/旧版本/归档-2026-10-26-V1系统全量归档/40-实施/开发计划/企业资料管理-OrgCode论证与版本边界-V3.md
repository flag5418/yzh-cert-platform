# 企业资料管理 · OrgCode 论证与版本边界 V3

> 2026-09-26｜回答你的第 2 条「这个还是得从文件存储结构中分析再论证」
> 并落实你的第 1 条（加 OrgCode）、第 3 条（标准库无版本 / 企业库有版本）、第 4 条（删 FileCode）
> 全部结论均经代码 + 数据库实测

---

## 一、你第 1 条的结论已采纳：加 OrgCode

你的理由我完全同意，而且比"格式可能不一样"更硬：

> 不能保证每个机构按标准和阶段的标准目录格式都是完全一样，我情愿重复建立也不会去冒险

**但实测发现：在现有表结构下，"每机构一份标准目录"根本做不到 —— 会撞唯一键。**

| 表 | 现有唯一键 | 问题 |
|---|---|---|
| `cert_standard_directory_config` | **`uk_standard_phase (StandardCode, PhaseCode)`** | **不含 OrgCode** → 机构 B 想建同一「标准+阶段」的目录 ⇒ **INSERT 撞唯一键 1062** |
| `cert_standard_directory_folder` | `uk_folder_code (FolderCode)`；`FolderCode = FD-{DirectoryCode}\|L{level}\|S{seq}`，`DirectoryCode = SDC-{Std}\|{Phase}` | **两处都不含 OrgCode** → 机构 B 为同一「标准+阶段」建文件夹 ⇒ **FolderCode 完全相同 ⇒ 撞唯一键 1062** |

**⇒ 你的诉求（每机构独立建）必须先改表结构与编码格式，否则第一步就报 1062。**

---

## 二、你第 2 条要的论证：文件存储是否需要 OrgCode？

你问：左树已经是「机构-标准-阶段」，右侧文件存储还需不需要 OrgCode 这个字段？

### 答案：**需要，三层理由，其中第一层是"不加就做不了"**

### 理由 1（硬约束 · 表结构层）

见 §一。**唯一键必须含 OrgCode**，否则每机构一份目录无法建立。
而且不只是加列 —— `FolderCode` 内嵌 `DirectoryCode`，所以 **`DirectoryCode` 本身必须含 OrgCode**，否则 `FolderCode` 永远撞车。

### 理由 2（业务层 · OrgCode 是"模板 ↔ 实例"的连接键）★ 最重要

企业库的文件夹结构**来自同一机构的标准目录**：

```
机构 A 的标准目录（iso40012016 / 03）→ 建出文件夹模板
        ↓  企业 A1 上传资料时，按这套模板建目录
企业 A1 的文件夹结构 = 机构 A 的标准目录结构
```

⇒ **要定位"用哪一套模板"，必须知道是哪个机构**。
`EnterpriseCode` 只能查到企业，查不到"该企业的标准目录模板属于哪个机构" —— 除非再 join 一次。

**OrgCode 是这个 join 的键。** 没有它，"企业结构与标准目录结构一致"这条诉求**无法表达**。

### 理由 3（代码层 · 后台流程没有左树上下文）

左树确实提供 OrgCode，但**写文件的是后台流程**：

| 场景 | 有无 HTTP 请求上下文 | 能否拿到左树选中的机构 |
|---|---|---|
| 页面上传（用户点了左树的某节点） | ✅ 有 | ✅ 能 |
| **转换链**（`OfficeConvertService`，队列执行器） | ❌ 无 | ❌ 不能 |
| **提取链**（`DocExtractionRuleService`） | ❌ 无 | ❌ 不能 |
| **回填/重试**（`BackfillConversions` / `RetryFailedConversions`） | ❌ 无 | ❌ 不能 |

现在只能靠 `DeriveOrgCodeFromPath` **反解路径** —— 而它已证明取错（见 §三）。

⇒ **必须落成显式列**，不能靠"左树上下文"或"解析路径"。

### 结论：三处都要加

| # | 位置 | 改法 |
|---|---|---|
| 1 | **表列** | `config` / `folder` / `file` 三张表各加 `OrgCode varchar(50)`（按铁律：列名 = C# 属性名 = PascalCase） |
| 2 | **编码** | `DirectoryCode` 从 `SDC-{Std}\|{Phase}` 改为 **`SDC-{OrgCode}\|{Std}\|{Phase}`** → `FolderCode` 自动继承，唯一键自然成立 |
| 3 | **路径段** | 见 §三 |

> 加列是**必须**的（理由 3）；路径段是**推荐**的（理由：MinIO 控制台人工排查可读）。

---

## 三、段序建议：`{OrgCode}` 放在最前

你说左树是「机构-标准-阶段」。**路径应与左树同序**：

```
标准库： /standard-directory/{OrgCode}/{StdCode}/{PhaseCode}/{FolderPath}/{FileName}
企业库： /enterprise-documents/{OrgCode}/{StdCode}/{PhaseCode}/{EnterpriseCode}/{FolderPath}/{FileName}
```

### ★ 这个段序有额外好处：现有代码反而变成对的

`CodeGeneratorService.cs:47` 声明的格式就是 `/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/…`。
`DeriveOrgCodeFromPath`（`StandardDirectoryService.cs:2124`）对 `standard-directory` 取 `idx=1`。

**⇒ 只要把实测数据补上 OrgCode 段，`idx=1` 就自然正确了，一行不用改。**

对比我上一轮（V2）建议的 `{StdCode}/{PhaseCode}/{OrgCode}` —— 那个顺序要让 `idx` 从 1 改成 3，且与左树顺序相反。**以本轮的为准。**

> ⚠️ 唯一遗留：`enterprise-documents` 分支的 `idx=2` 需要按最终企业库段序确定。
> 若企业库 = `{OrgCode}/{StdCode}/{PhaseCode}/{EnterpriseCode}/…`，则机构段仍在 `idx=1`，
> **两个分支可以统一成 `idx=1`**。

---

## 四、★★ 新发现：全项目有三种「标准编码」约定，互不相同

论证过程中实测到的，**这是企业库落地前的头号陷阱**：

| 表 | 列 | 实测值 | 约定 |
|---|---|---|---|
| `cert_iso_standard` | `Code` | `846dec4b-c534-4983-94e6-8cf04982b7d9` | GUID |
| `cert_iso_standard` | **`StandardCode`** | **`iso9001-2015`** | **人读码（权威源）** |
| `cert_org_standard`（左树） | `StandardCode` | `846dec4b-…` | **GUID** |
| `cert_enterprise_stage` | `StandardCode` | GUID | **GUID** |
| `cert_standard_directory_config` | `StandardCode` | **`iso90012015`** | **`CleanCode(人读码)`** ← 破折号被删 |

实测证据：
- `cert_org_standard` 两行：`OrgCode=1579641b…` / `906e8b2a…`，`StandardCode=475da4fe-…` / `846dec4b-…`（**GUID**）
- `cert_standard_directory_config` 两行：`StandardCode=iso40012016` / `iso90012015`（**无人读码、无破折号**）
- `cert_iso_standard`：`Code=846dec4b-…`，`StandardCode=iso9001-2015`

### ⚠️ 影响

**企业库路径里的 `{StdCode}` 取哪个？**

- 取左树的 **GUID** → 路径变成 `/…/846dec4bc534498394e68cf04982b7d9/…` —— 人不可读，且**与文件夹模板对不上**
- 取目录配置的 `iso90012015` → 与模板对得上，但**左树给的是 GUID**，中间要三次跳转：
  ```
  左树 GUID → cert_iso_standard.Code → cert_iso_standard.StandardCode(iso9001-2015)
            → CleanCode → iso90012015 → 匹配 cert_standard_directory_config
  ```

**⇒ 建议：在 `cert_standard_directory_config` 增加 `IsoStandardCode`（存 `cert_iso_standard.Code` 的 GUID）列**，把这个三跳映射显式化。
否则每次都要字符串清洗，**且 `CleanCode` 会删 `-`/`:`/空格，是单向不可逆的** —— 将来若标准编码形态变化（如 `ISO 9001:2015`），映射会静默错配。

> 📌 同时注意：左树 `cert_org_stage` 的 **`StandardCode` 全是 `NULL`** —— 阶段是按机构挂的，不是按 (机构,标准) 挂的。左树"机构→标准→阶段"的第三级实际只按 OrgCode 过滤。这一点在实现企业库时需要确认是否符合预期。

---

## 五、你第 3 条：标准库无版本 / 企业库有版本 → 方案分叉

> 标准目录没有版本号的概念，这个和企业目录不一样，针对标准目录只有单纯的覆盖，既然是标准，就没办法去管理版本的问题。但企业目录不一样，企业的文件会经常被替换

**这条把 V2 的归档机制适用范围收窄了一半，是重要的范围削减。**

| 能力 | 标准库 | 企业库 |
|---|---|---|
| 上传 | 单纯覆盖（**现状行为不变**） | 覆盖 + 归档 + 版本 |
| 历史表 | ❌ 不需要 | ✅ `cert_enterprise_doc_file_history`（或复用同一张，见下） |
| 归档文件夹 | ❌ 不需要 | ✅ `_archive/`（源 + pdf + markdown 三处） |
| `FileHash` | ❌ 不需要 | ✅ **需要**（追溯"文件是否被改过"的硬证据） |
| 版本号列 | ❌ 不需要 | ✅ 需要 |

### ★ 这带来一个必须澄清的设计问题

`DocumentLibrary.cs:11-17` 的原始设计意图是：

> 企业资料上传与标准目录上传**过程一致**（同样是「文件夹 + 文件 + 转 PDF + 转 Markdown」），
> 因此转换链、预览链、提取链应当**共用同一套实现**，**仅靠输入路径前缀区分归属**。

但按你第 3 条，**上传过程不再一致**（一个覆盖、一个归档+版本）。
所以：

| 选项 | 含义 | 影响 |
|---|---|---|
| **A** | **仍复用 `cert_standard_directory_file`** 一张表，版本列对标准库留空不用；转换/预览/提取链零改动 | 表变"胖"（标准库用不到版本列）；但**共用链**成立，改动最小 |
| **B** | **企业库新建独立表**（`cert_enterprise_doc_file` 等），与标准库分开 | 表干净；但转换/预览/提取链要改成泛型或双实现 —— **代价大** |

**我建议 A**：理由是 `DocumentLibrary` 的设计意图是"共用链"，而链的价值远大于表整洁。
版本列在标准库侧恒为空/不写即可（用一个 `Library` 判别列或直接按路径前缀判断）。

> **需要你定**：A（复用一张表）还是 B（分表）？

---

## 六、你第 4 条：删 `FileCode` —— 已确认，建议与迁移合并做

你说「我情愿用规则来匹配文件，不想用一个复杂的编码来判断文件是否重复」。

**同意。而且现在做它比单独做便宜** —— 因为 §一的 OrgCode 改造**本来就要重写 `DirectoryCode` / `FolderCode` / `FileCode`**（334 行文件 + 22 行文件夹 + 2 行配置）。

**⇒ 建议合并成一次迁移**：

```
一次迁移脚本：
  ① config / folder / file 三表加 OrgCode 列并回填
  ② DirectoryCode:  SDC-{Std}|{Phase}  →  SDC-{OrgCode}|{Std}|{Phase}
  ③ FolderCode:     FD-{DirectoryCode}|L{level}|S{seq}   （自动继承，重算）
  ④ 唯一键：uk_standard_phase → uk_org_standard_phase(OrgCode, StandardCode, PhaseCode)
             uk_directory_code  → uk_org_directory_code(OrgCode, DirectoryCode)
             uk_folder_code     → 保持（因 FolderCode 已含 OrgCode）
  ⑤ DROP FileCode 列；全部引用改用 Code
  ⑥ MinIO：334 个对象从 /{Std}/{Phase}/… 移到 /{OrgCode}/{Std}/{Phase}/…
```

**分两次做的代价**：OrgCode 迁移一次（改 334 行编码）+ 删 FileCode 一次（再动同一批行）—— **同样的数据动两遍**。

> 唯一顾虑：删 `FileCode` 是破坏性的。建议脚本**先备份受影响行**（`CREATE TABLE ..._bak_20260926 AS SELECT`），验证后再清理。

---

## 七、修订后的阶段划分

| 阶段 | 内容 | 变化 |
|---|---|---|
| **P0** | 统一 `PathBuilder`（含 OrgCode 段，标准库/企业库）+ **修 `DeriveOrgCodeFromPath` 统一 `idx=1`** | 不变 |
| **P1** | **一次性迁移**：加 OrgCode 列 + 重写 DirectoryCode/FolderCode + 改唯一键 + 删 FileCode + 移 334 个 MinIO 对象 | ★ **本轮升级**（原 P1 是归档机制） |
| **P2** | `/resources` 页面（左树 机构-标准-阶段 / 右文件浏览） | 左树已存在（`cert_org_standard`/`cert_org_stage` 有 2 个机构数据） |
| **P3** | **企业库专属**：归档机制 + 历史表 + FileHash + 版本号 | ★ **范围收窄**（标准库不做） |
| **P4** | 企业资料上传（N 份副本，先 F1） | |
| **P5** | 提取链接入企业库（放开 `DocumentLibrary` 白名单，链零改动） | |
| **P6** | `cert_standard_directory_config` 加 `IsoStandardCode`（GUID）列，显式化三跳映射 | ★ **新增**（§四） |
| **P7** | A1 `CopyObjectAsync` / F2 产物复制 | **可以不做**（实测文件最大 359KB） |

**关键路径：P0 → P1 → P3 → P4。**

---

## 八、待你裁决（本轮新增 3 项）

| # | 问题 | 选项 | 我的建议 |
|---|---|---|---|
| **④** | 企业库的表策略 | A 复用 `cert_standard_directory_file`（共用转换/预览/提取链）／B 新建独立表 | **A** |
| **⑤** | 段序最终确认 | `{OrgCode}/{StdCode}/{PhaseCode}/…`（与左树同序）／`{StdCode}/{PhaseCode}/{OrgCode}/…`（上一轮 V2） | **前者**（`DeriveOrgCodeFromPath` 的 `idx=1` 不用改） |
| **⑥** | 迁移与删 `FileCode` 是否合并 | 合并一次迁移／分两次 | **合并**（同一批数据只动一遍） |

**上一轮遗留的 ③（归档触发条件：内容变了才归档 vs 每次上传都归档）仍需你定** —— 现在它只影响企业库，范围更小了。

---

## 九、结论速览

1. **你第 1 条（加 OrgCode）—— 同意，但不改表就撞唯一键**，必须同时改 `uk_standard_phase` 与 `DirectoryCode` 格式
2. **你第 2 条（文件存储是否需要 OrgCode）—— 需要，三层理由**：唯一键硬约束 / 模板↔实例的连接键 / 后台流程没有左树上下文
3. **段序建议 `{OrgCode}` 在最前** —— 与左树同序，且让 `DeriveOrgCodeFromPath` 的 `idx=1` 自然正确
4. **★ 新发现：三种 `StandardCode` 约定互不相同**，企业库路径取哪个必须先定，建议加 `IsoStandardCode` 列显式化
5. **你第 3 条（标准库无版本）—— 收窄了归档机制范围**，但引出「企业库是否复用同一张表」的新决策
6. **你第 4 条（删 FileCode）—— 同意，且建议与 OrgCode 迁移合并成一次**（同一批数据只动一遍）
