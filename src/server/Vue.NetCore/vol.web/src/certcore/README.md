# certcore —— 认证平台项目级通用层

> **定位**：认证业务域通用组件与方法库。与框架级 `yzh/` 分离：`yzh/` 可整体剥离支撑其他项目，`certcore/` 依赖认证业务模型，仅本项目使用。

## 目录结构

```
certcore/
├── README.md
├── index.js                  # 统一出口（barrel）
├── styles/
│   └── cert-tokens.css       # 业务扩展令牌（层级色/文件类型色/规则状态色）
├── icons/
│   └── index.js              # 业务图标映射（目录树层级/文件类型/状态）
├── components/
│   ├── CertDirectoryTree.vue # ★ 目录树：机构→标准→阶段→文件夹→文件（懒加载）
│   ├── CertDocPreview.vue    # ★ 文档预览（规划：从 DocExtractionRule 提升）
│   ├── CertConvertBadge.vue  # 转换状态徽标
│   ├── CertStatusBar.vue     # 底部状态栏
│   ├── CertPageHeader.vue    # 页面标题栏
│   └── index.js
├── composables/
│   ├── useFileTree.js        # 树数据转换/懒加载/目录编码
│   ├── useDirectoryApi.js    # 标准目录 API 封装
│   └── usePolling.js         # 轮询
└── utils/
    ├── format.js             # formatFileSize / formatDate
    ├── api.js                # API 响应解包（unwrap）
    ├── download.js           # blob 下载 + 文件名解析
    └── convertStatus.js      # 转换状态映射
```

## 引用方式

```js
// 组件 / composables / utils / icons 统一经 index.js
import { CertDirectoryTree, useFileTree, formatFileSize } from '@/certcore'
// 或按需经子 barrel
import { CertDirectoryTree } from '@/certcore/components'
```

```css
/* 样式：先 yzh 令牌，再 certcore 业务令牌 */
@import '@/yzh/styles/yzh.css';
@import '@/certcore/styles/cert-tokens.css';
```

## 使用规范

1. **图标**：经 `@/yzh` 的 YzhIcon / 本目录 CertTreeIcon 语义命名，禁止直接 import 图标库
2. **样式**：禁止硬编码色值/间距，一律取 `--yzh-*` / `--cert-*` 变量
3. **新增通用件**：业务页面出现第二处重复时，提升到本目录并同步本 README
