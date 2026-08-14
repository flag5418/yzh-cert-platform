# OSS 存储结构重新设计 V1

> **文档状态**: 待审核  
> **创建日期**: 2026-08-14  
> **关联文档**: `审核员注册-企业创建-文件存储链路差异分析-V1.md`、`数据库表设计-V2.md`

---

## 一、审核员实际业务流程（确认）

```
审核员注册 → 建立企业资料 → 新建任务（选标准+选阶段） → 上传资料
```

### 1. 审核员注册
- 填写：登录名 + 密码 + 手机号（或手机验证码注册）
- 选择：一个体系认证机构
- 登录名和密码等基本信息

### 2. 审核员进入后
- **建立企业资料**：企业绑定在审核员下
- 即使同名企业，在不同审核员下也是**两个独立企业**

### 3. 开始建立任务
- 新建任务 → 选择一个标准 → 选择一个阶段 → 新建任务

### 4. 上传资料
- 基于标准目录结构上传

---

## 二、当前 MinIO 存储现状

### 2.1 当前路径结构

```
cert-platform/                         ← Bucket
└── CB001/                             ← 认证机构编码（OrgCode）
    └── ISO134852016/                  ← 标准编码（StandardCode）
        └── STAGE01/                   ← 阶段编码（PhaseCode）
            ├── CS河北雄安尚龙医疗科技有限公司13485体系材料/   ← 根文件夹名（企业名称混入）
            │   ├── 1质量手册/
            │   │   └── XASL-QM 质量手册.docx
            │   ├── 2程序文件/
            │   ├── 4记录文件/
            │   └── 陪审人员.doc
            └── E_Documents except for the above parts (2).pdf
```

### 2.2 当前路径生成逻辑（CodeGeneratorService.cs）

```csharp
// 当前 V2 路径生成
public string GenerateStoragePathV2(string orgCode, string standardCode, string phaseCode,
                                    string folderPath, string fileName)
{
    // 格式：/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
    // 示例：/CB001/ISO134852016/STAGE01/质量手册/程序文件.docx
    return $"/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
}
```

### 2.3 当前存在的问题

| 问题 | 说明 |
|------|------|
| **❌ 企业名称混入标准目录** | 路径中 `CS河北雄安尚龙医疗科技有限公司13485体系材料` 是企业名称，不是标准目录结构 |
| **❌ 标准目录与企业资料未分离** | 当前只有一种存储路径，标准目录和企业上传资料混在同一个路径下 |
| **❌ 没有审核员维度** | 无法区分是哪个审核员上传的企业资料 |
| **❌ 没有企业编码维度** | 无法区分是哪个企业的资料 |
| **❌ 没有任务维度** | 无法区分文件属于哪个审核任务 |

---

## 三、用户提出的 OSS 存储新方案

### 3.1 核心思路：两个顶层文件夹

```
cert-platform/                         ← Bucket
├── 标准目录/                           ← 顶层文件夹 1：标准目录
│   └── {认证机构}/{标准}/{阶段}/{文件夹}/{文件}
│
└── 企业资料/                           ← 顶层文件夹 2：企业资料
    └── {企业编码}/                     ← 一个企业一个编码
        └── {认证机构}/{标准}/{阶段}/{文件夹}/{文件}   ← 结构与标准目录一致
```

### 3.2 用户原话整理

> 1. 企业资料：一个大的文件夹，下面是所有企业的资料
> 2. 企业编码：一个企业一个编码，用企业的 code，该企业的所有资料都在这个下面
> 3. 认证机构-标准-阶段-文件夹-文件：这个结构就和标准目录结构一致了
>
> 标准目录可以在当前结构的顶部增加一个标准目录的大文件夹，下面存储各种机构、各种标准、各种阶段下的不同文件夹
>
> 相当于 OSS 中有两个大的文件夹：1.标准目录 2.企业资料
>
> 这样即可确保标准目录和企业目录的结构在某一级下是一致的

### 3.3 具体路径示例

**标准目录路径：**
```
/标准目录/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
示例：
/standard-directory/CB001/ISO134852016/STAGE01/1质量手册/XASL-QM 质量手册.docx
```

**企业资料路径：**
```
/企业资料/{EnterpriseCode}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
示例：
/enterprise-documents/086badc8-8ef1-11f1-a2e2-e6bd9a193da2/CB001/ISO134852016/STAGE01/1质量手册/质量手册.docx
```

---

## 四、合理性分析

### 4.1 ✅ 优点（5 个）

| # | 优点 | 说明 |
|---|------|------|
| 1 | **标准目录与企业资料物理隔离** | 两个顶层文件夹完全隔离，标准目录只存模板/标准文件，企业资料只存企业上传文件，不会混在一起 |
| 2 | **路径结构在某一级之后完全一致** | 从 `{OrgCode}/{StandardCode}/{PhaseCode}/...` 开始，标准目录和企业资料的路径结构完全相同，便于对比校验 |
| 3 | **企业维度隔离清晰** | 每个企业有自己的 `EnterpriseCode` 文件夹，企业的所有资料都在这个文件夹下，不同企业资料完全隔离 |
| 4 | **支持多审核员同名企业** | 因为企业绑定在审核员下，每个企业有独立的 EnterpriseCode，同名企业自然隔离 |
| 5 | **易于扩展** | 未来如果需要增加审核员维度，可以在 `企业资料/` 下增加 `{AuditorCode}/` 层级 |

### 4.2 ⚠️ 需要确认的设计点（4 个）

| # | 设计点 | 选项 A | 选项 B | 建议 |
|---|--------|--------|--------|------|
| Q1 | **顶层文件夹命名** | 中文：`标准目录` / `企业资料` | 英文：`standard-directory` / `enterprise-documents` | **建议 B（英文）**：MinIO 路径建议用 ASCII 字符，避免中文编码问题 |
| Q2 | **企业资料是否需要审核员维度** | `企业资料/{EnterpriseCode}/...`（不包含审核员） | `企业资料/{AuditorCode}/{EnterpriseCode}/...`（包含审核员） | **建议 A（不含审核员）**：企业已有 EnterpriseCode 唯一标识，审核员信息在数据库中关联即可，路径不需要冗余 |
| Q3 | **任务维度是否体现在路径中** | 路径包含 `{TaskCode}`：`企业资料/{EnterpriseCode}/{OrgCode}/{StandardCode}/{PhaseCode}/{TaskCode}/...` | 路径不包含 TaskCode，任务信息在数据库中管理 | **建议不含 TaskCode**：同一企业同一标准同一阶段的文件，不应因任务不同而路径不同。任务信息在数据库中通过 `TaskId` 字段关联 |
| Q4 | **转换后文件的路径** | `.converted/` 隐藏目录（当前方案） | `_converted/` 普通目录 | **保持现状**：`.converted/` 是标准做法 |

### 4.3 🔍 深度分析

#### 4.3.1 标准目录的定位问题

当前 MinIO 中存储的文件实际上是**企业上传的文件**（如 `CS河北雄安尚龙医疗科技有限公司13485体系材料`），而不是**标准目录模板**。

用户的新方案将两者分离：
- **标准目录**：存储标准要求的目录结构定义（文件夹层级、文件要求等），可能只需要存数据库记录，不一定需要实际文件
- **企业资料**：存储企业实际上传的文件

**关键问题**：标准目录是否需要存储实际文件？

> 如果标准目录只是定义"应该有哪些文件夹和文件"的模板，那么标准目录在 OSS 中可能不需要顶层文件夹，只需要在数据库中维护目录结构。但考虑到标准文件本身也可能有参考文件（如标准原文），保留一个标准目录的存储空间也是合理的。

#### 4.3.2 企业编码的选择

当前 `cert_enterprise` 表的 `Code` 字段是 UUID（如 `086badc8-8ef1-11f1-a2e2-e6bd9a193da2`），作为 OSS 路径太长。

建议：
- 使用 `EnterpriseCode` 字段（可新增一个短编码字段，如 `ENT0001`）
- 或者直接用 UUID（路径长但唯一性保证好）

#### 4.3.3 与审核员流程的关系

```
审核员注册 → 获得审核员 Code (如 AUD-001)
    ↓
建立企业 → 企业获得 EnterpriseCode (如 ENT-2026-0001)
    ↓
新建任务 → 任务获得 TaskCode (如 TASK-2026-0001)
    ↓
上传资料 → 路径：企业资料/{EnterpriseCode}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
```

注意：**审核员的认证机构（OrgCode）在审核员注册时确定**，所以企业资料路径中的 `{OrgCode}` 就是审核员所属的认证机构编码。

---

## 五、最终推荐的 OSS 存储路径方案

### 5.1 路径结构

```
cert-platform/                                          ← Bucket
│
├── standard-directory/                                 ← 标准目录（模板/参考文件）
│   └── {OrgCode}/                                      ← 认证机构编码
│       └── {StandardCode}/                             ← 标准编码
│           └── {PhaseCode}/                            ← 阶段编码
│               └── {FolderPath}/                       ← 文件夹路径
│                   └── {FileName}                      ← 文件名
│
└── enterprise-documents/                               ← 企业资料（企业上传文件）
    └── {EnterpriseCode}/                               ← 企业编码
        └── {OrgCode}/                                  ← 认证机构编码
            └── {StandardCode}/                          ← 标准编码
                └── {PhaseCode}/                         ← 阶段编码
                    └── {FolderPath}/                   ← 文件夹路径
                        ├── {FileName}                  ← 原始文件
                        └── .converted/{FileName}       ← 转换后文件
```

### 5.2 具体示例

**标准目录示例：**
```
/standard-directory/CB001/ISO134852016/STAGE01/1质量手册/XASL-QM 质量手册模板.docx
```

**企业资料示例：**
```
/enterprise-documents/ENT-2026-0001/CB001/ISO134852016/STAGE01/1质量手册/XASL-QM 质量手册.docx
```

**转换后文件：**
```
/enterprise-documents/ENT-2026-0001/CB001/ISO134852016/STAGE01/1质量手册/.converted/XASL-QM 质量手册.docx
```

### 5.3 路径对比（标准目录 vs 企业资料）

```
标准目录：  /standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
企业资料：  /enterprise-documents/{EnterpriseCode}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
差异：      顶层目录名不同 + 企业资料多了 {EnterpriseCode} 一层
一致部分：  {OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName} 完全一致
```

---

## 六、需要代码修改的部分

### 6.1 CodeGeneratorService.cs 修改

```csharp
// 新增方法：生成标准目录存储路径
public string GenerateStandardDirectoryPath(string orgCode, string standardCode, 
                                            string phaseCode, string folderPath, string fileName)
{
    var cleanOrg = CleanCode(orgCode);
    var cleanStandard = CleanCode(standardCode);
    var cleanPhase = CleanCode(phaseCode);
    var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";
    
    if (string.IsNullOrEmpty(cleanFolderPath))
        return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";
    
    return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
}

// 新增方法：生成企业资料存储路径
public string GenerateEnterpriseDocumentPath(string enterpriseCode, string orgCode, 
    string standardCode, string phaseCode, string folderPath, string fileName)
{
    var cleanEnt = CleanCode(enterpriseCode);
    var cleanOrg = CleanCode(orgCode);
    var cleanStandard = CleanCode(standardCode);
    var cleanPhase = CleanCode(phaseCode);
    var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";
    
    if (string.IsNullOrEmpty(cleanFolderPath))
        return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";
    
    return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
}
```

### 6.2 数据迁移

当前 MinIO 中的文件需要迁移：
- `/CB001/ISO134852016/STAGE01/...` → `/standard-directory/CB001/ISO134852016/STAGE01/...` 或 `/enterprise-documents/{EntCode}/CB001/ISO134852016/STAGE01/...`

---

## 七、审核员完整流程与存储路径对应关系

```
1. 审核员注册
   → 审核员信息写入 Sys_User (UserType=22, OrgCode=CB001)
   → 不涉及文件存储

2. 审核员建立企业
   → 企业信息写入 cert_enterprise (Code=ENT-UUID, OrgCode=审核员的OrgCode)
   → 不涉及文件存储

3. 审核员新建任务
   → 任务信息写入 cert_upload_task (TaskId, DirectoryCode)
   → 不涉及文件存储

4. 审核员上传资料
   → 文件上传到 MinIO
   → 路径：/enterprise-documents/{EnterpriseCode}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
   → 文件记录写入 cert_standard_directory_file (StoragePath=上述路径)
```

---

## 八、总结

| 维度 | 评价 |
|------|------|
| **方案合理性** | ✅ 非常合理。两个顶层文件夹分离标准目录和企业资料，结构清晰 |
| **路径一致性** | ✅ 标准目录和企业资料在 `{OrgCode}/{StandardCode}/{PhaseCode}/...` 之后完全一致 |
| **企业隔离** | ✅ 通过 `{EnterpriseCode}` 实现企业级隔离 |
| **审核员隔离** | ✅ 通过企业绑定审核员实现间接隔离（企业 Code 唯一） |
| **扩展性** | ✅ 未来可在 `enterprise-documents/` 下增加 `{AuditorCode}/` 层级 |
| **实施难度** | ⚠️ 中等。需要修改路径生成逻辑 + 迁移现有 MinIO 文件 + 更新数据库记录 |

### 待用户确认

1. **顶层文件夹命名**：英文 `standard-directory` / `enterprise-documents` 还是中文？
2. **企业编码格式**：用 UUID 还是短编码（如 `ENT-2026-0001`）？
3. **是否需要审核员维度**：路径中是否包含 `{AuditorCode}`？
4. **任务维度**：路径中是否包含 `{TaskCode}`？（建议不包含）
5. **现有数据迁移**：是否需要迁移现有 MinIO 文件到新路径？
