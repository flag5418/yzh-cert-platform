# 2026-08-10 DocExtractionRule 预览链 12 类踩坑与根因修复汇总

**日期**：2026-08-10
**涉及模块**：`DocExtractionRule/`（DocPreview.vue / FileTree.vue / index.vue）、`http.js` 封装、`StandardDirectoryController.DownloadFile`
**真实环境**：后端 API :9992 / 后台管理 :9990 / 审核员前端 :9991 / MinIO :9000 MySQL :3307 Redis :6380
**真实样例文件**：
- `XASL-PR-027 生产过程自检记录.xlsx`（16922 bytes，真实合法 OOXML xlsx）
- `第一类医疗器械适用法律法规目录.xlsx`（用户 Network Hex 100% 合法 xlsx 结构）
- `XASL-QP-030 医疗器械不良事件报告和再评价程序.docx`（真实合法 OOXML docx 结构）

---

## 0. 锚点链路（定位必用，100% console.log，Default levels 必现）

按 Chrome DevTools Console 过滤 `[FileTree] / [DocExtractionRule] / [DocPreview]`，7 个锚点日志缺一不可：

```
① [FileTree] onNodeClick: {type:'file'|'folder'|'stage', id, name}
② [FileTree] ✅ emit select → <id> <name>                       (只有 type='file' 才触发)
③ [DocExtractionRule] ✅ onFileSelect 触发: {id,name,type,storagePath,mimeType}
④ [DocPreview] 🔵 script setup 已初始化 ✅                     (没出现 = 组件根本没挂载 / @vue-office import 抛异常 / 热更新失败)
⑤ [DocPreview] 🟢 watcher 已注册 ✅ immediate=true → loadPreview 已同步执行 (没出现 = defineProps/computed 之前代码 throw)
⑥ [DocPreview] 🟡 开始预览 <文件名>  URL=/api/standard-directory/download?path=...
⑦ [DocPreview] 二进制下载完成, byteLength=...
   [DocPreview] 魔数检测：{redirect:false, kind:'zip-or-ooxml'}
   [DocPreview] ZIP Central Directory 子类型检测：ext=xlsx detected=xlsx
   [DocPreview] 🟢✅ 已交付给 vue-office：ext=xlsx byteLength=... kind=zip-or-ooxml
   [DocPreview] rendered: <文件名>                                              (vue-office @rendered 回调)
```

**严重教训**：所有调试日志必须统一用 `console.log`，Chrome Default levels 会直接隐藏 `console.debug`。上一轮全部用 `console.debug` 导致用户所有 `[DocPreview]` 日志一条都看不到，浪费了 2 轮对话。

---

## 1. http.js `res?.data` 多套一层 undefined（E1，P0）

### 问题描述
真实下载 docx/xlsx Network Hex 100% 合法（504B0304 ZIP 头 + 标准 Central Directory），但前端始终进入「服务器返回空文件」降级分支，previewBuffer 永远是 undefined。

### 根因分析
[http.js#L145-L164](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/api/http.js#L145-L164) 内部实现是：
```js
axios.get(url, config).then(
  (response) => { resolve(response.data) }   // ← 只 resolve 了 data，不是整个 response
)
```
而 DocPreview.vue 原来写的是：
```js
const res = await http.get(url, null, false, { responseType:'arraybuffer' })
const buf = res?.data   // ← res 本身就是 response.data（ArrayBuffer），再 .data 一层恒等于 undefined
```

### 解决方案
统一包装 3 个专用方法，**不加 `.data`**：
```js
const _httpGetArrayBuffer = url => http.get(url, null, false, { responseType:'arraybuffer' })
const _httpGetBlob        = url => http.get(url, null, false, { responseType:'blob' })
const _httpGetText        = url => http.get(url, null, false, { responseType:'text' })

// 使用：
let buf = await _httpGetArrayBuffer(url)    // ✅ buf 直接是真实 ArrayBuffer，byteLength>0
```

### 预防措施
- 所有调用 `http.get/post` 的地方先查 http.js 的 resolve 实现，先打印 `typeof res` / `res?.byteLength`，确认返回类型再用
- **禁止** 在 http.js 封装返回 data 的代码里再写 `.data` 一层

---

## 2. 原生 fetch 缺 Vol Authorization Header → 401 → ASP.NET Core 重定向登录页 HTML → JSZip central directory 报错（E2，P0）

### 问题描述
DocPreview.vue 最开始用原生 `fetch(url, { credentials:'include' })` 下载 ArrayBuffer，Network 返回 `200 OK`，但 Content-Type 是 `text/html; charset=utf-8`，喂给 `@vue-office-docx` 后 JSZip 抛 `Can't find end of central directory : is this a zip file ?`

### 根因分析
Vol 框架鉴权是 `[JWTAuthorize]`，请求头需要 `Authorization: Bearer <token>`，原生 fetch 只带 Cookie 不会带这个 Header，后端直接 401，而 ASP.NET Core 默认对 401 做 302 重定向到 `/login`，fetch 跟随重定向返回一个登录页 HTML，字节流本身以 `<!doctype html` 开头，HTTP 状态仍是 200，所以 JSZip 当 ZIP 解压直接炸。

### 解决方案
**100% 复用 http.js 封装**：它在每次 get 之前会自动执行：
```js
axios.defaults.headers[_Authorization] = getToken()
setHeaderLang(axios.defaults.headers)
```
token / lang / baseURL 自动注入。原生 fetch **绝对不要再用**。

### 预防措施
- 整个前端任何地方要发请求，**一律走 `import http from '@/api/http'`**，禁止原生 fetch / axios.create 单独起实例
- http.js headers 本身不可见（只 resolve data），所以「Content-Type 是否为 text/html」的判断方式必须改为「魔数 + payload 头 256 bytes 文本匹配」（见坑 3）

---

## 3. Content-Type 不可见 → 改用「魔数白名单 + payload 头 256 bytes HTML 特征匹配」替代判断登录重定向（P1）

### 问题描述
因为 http.js 只 `resolve(response.data)`，`Content-Type / Content-Disposition / Status` 这些 headers 在业务层根本拿不到。原来的分支判断 `if (contentType.indexOf('text/html') >= 0) previewError='登录失效'` 永远不会触发。

### 根因分析
http.js 封装时只取了 data，把 headers 全丢了（没法改，因为这是 Vol 框架自带的 http.js，不能动封装层）。所以所有基于 headers 的判断必须改为基于 **payload 字节本身** 的判断。

### 解决方案
写一个独立函数 `_looksLikeAuthRedirect(buf)`：
```js
// Layer 1：合法二进制魔数白名单（直接通过，不会误判）
const magic32 = (u8[0]<<24)|(u8[1]<<16)|(u8[2]<<8)|u8[3]
if (magic32 === 0x504B0304) return { redirect:false, kind:'zip-or-ooxml' }
if (magic32 === 0x25504446) return { redirect:false, kind:'pdf' }
if (u8[0]===0x89 && magic32==0x89504e47) return { redirect:false, kind:'png' }
/* 其他 JPG / GIF / BMP / WebP 同理 */

// Layer 2：不是合法二进制 → 读前 256 bytes 转 UTF-8 小写，看 HTML / JSON 登录特征
const head = new TextDecoder('utf-8').decode(u8).toLowerCase()
if (/<!doctype|<html|<head|登录/i.test(head)) return { redirect:true, kind:'html' }
if (u8[0]===0x7b && /"code"|login|unauthorized|登录/i.test(head)) return { redirect:true, kind:'json' }
return { redirect:false }
```
- PDF 头是 `%PDF-`（`25 50 44 46`）→ 100% 能识别
- HTML / JSON 登录页头 256 bytes 必然包含 `<html` 或 `登录` 或 `"code":` → 不会漏
- 合法 docx/xlsx ZIP 头必然是 `PK..`（50 4B 03 04）→ 不会误判

### 预防措施
- 任何调用 http.js 的地方，只要想判断「返回的是文件还是业务报错 / 重定向」，一律用 payload 字节本身判断，不要依赖 headers
- 白名单模式永远比黑名单模式安全，先判合法二进制魔数，再判文本登录特征

---

## 4. DocExtractionRule 三栏高度坍缩（只占顶部 ~250px）+ element-plus 19 条 Setup/Update Proxy 空报错（E4，P1）

### 问题描述
DocExtractionRule 三栏（左树 / 中预览 / 右 Tabs）高度只占最顶部 ~250px，下面全是空白，并且 Console 里出现 19 条 element-plus.js 的 Setup/Update 访问 null Proxy 的报错。

### 根因分析
Vol 框架自带 `el-scrollbar__view` / `router-view` 外层没有显式 height，所以：
```css
/* 根容器写了 height:100% → 父链 height=0 → 实际是 0 → flex 坍缩到内容高度（≈250px） */
.doc-extraction-rule { height: 100%; display:flex; flex-direction:column }
```
同时内部组件在 `onMounted` 里拿 `clientHeight` 计算布局时拿到的是 0，访问空节点属性直接返回空 Proxy → 19 条报错。

### 解决方案
**六层 absolute + min-height:0 高度链**（从根到 vue-office 组件逐层打通）：
1. 根容器：`position:absolute; top:16px; left:24px; right:24px; bottom:16px; background:#f5f7fa;`（绕开 el-scrollbar 父链无 height 的 bug）
2. `.main-container`：`flex:1; min-height:0`（子 flex 容器必须 min-height:0，否则孙子节点的 height:100% 参考还是 0）
3. 三栏容器 el-row / el-col：`height:100%`
4. `.doc-preview` / `.empty-preview`：`flex:1; min-height:0`
5. `.preview-content`：`flex:1; min-height:0; overflow:auto`
6. vue-office 子组件（`.vue-office-docx / .docx-wrapper / .excel-table / .vue-office-pdf`）：`height:100%; min-height:0`

### 预防措施
- 任何需要「填满整个可视区」的 Vol 子页面，一律不用 height:100%，直接用 absolute top/left/right/bottom 锚定到父容器
- 只要用了 flex 嵌套，**每一级 flex 子容器必须加 min-height:0**，这是 W3C flex 规范规定的默认 min-height:auto 会导致子孙子节点 height:100% 参考坍缩，是前端 80% 高度坍缩问题的根因

---

## 5. DirectoryManager（DirectoryConfig 路由）四周 padding 视觉消失（E3，P1）

### 问题描述
DirectoryConfig 路由映射 DirectoryManager 页面，AGENTS.md 要求 7 个 CertPlatform 页面统一 padding 16/24，用户反馈「我重启了还是没看到 padding」。

### 根因分析
整个 DirectoryManager 页面全是白背景，absolute top:16 left:24 right:24 bottom:16 的留白在周围全是白背景的情况下**肉眼不可见**，用户以为没生效。

### 解决方案
- 根容器 `background:#f5f7fa; gap:16px;`（浅灰对比色）
- 左右两张卡片 `background:#fff; border:1px solid #e4e7ed; border-radius:4px;`（白色卡 + 细边框）
→ 留白立刻可辨，即使不打开 DevTools 也能看出四周有 16/24 的 padding

### 预防措施
- 只要 padding / margin 是要求可见的，就要给父容器和子容器**不同的背景色 + 对比边框**，否则纯白背景下留白是看不见的
- AGENTS.md 规定「四周 padding」的语义是「留白要让用户能看出，不是只在盒模型里生效」

---

## 6. @vue-office 官方支持矩阵分支错写（xls 归为旧格式 / 缺 pptx 分支）（E6，P1）

### 问题描述
最开始分支是 `if (ext==='xlsx')` 才支持，.xls 直接走旧格式不支持降级；.pptx 完全没分支，.doc 和 .ppt 降级提示也没写。

### 根因分析
未按用户提供的 [关键信息速查.md](file:///Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/关键信息速查.md) 中 2025-05 GitHub vue-office/vue-office README 官方支持矩阵写分支。真实矩阵是：

| 扩展名 | 是否支持 | 官方依据 |
|---|---|---|
| .docx | ✅ 支持 | @vue-office/docx 底层 docx-preview，只解析 OOXML |
| .xlsx | ✅ 支持 | @vue-office/excel 底层 exceljs OOXML 解析 |
| .xls  | ✅ 支持 | exceljs BIFF 兼容模式，不用转格式直接解析 |
| .pptx | ✅ 支持 | 同 OOXML + JSZip 架构，直接复用 @vue-office/docx 组件渲染 |
| .pdf  | ✅ 支持 | @vue-office/pdf 底层 PDF.js |
| .doc  | ❌ 不支持 | OLE2 二进制格式，@vue-office 没有对应组件 |
| .ppt  | ❌ 不支持 | OLE2 二进制格式，@vue-office 没有对应组件 |

### 解决方案
分支按下面顺序写（**注意顺序：isOldFormat 必须在 isExcel 和 isDocx/isPptx 之后判断，防止 .xls 和 .ppt 被 isOldFormat 提前吞掉**）：
```js
const isDocx    = computed(() => ext.value === 'docx')
const isPptx    = computed(() => ext.value === 'pptx')   // 新增
const isExcel   = computed(() => ['xlsx','xls'].includes(ext.value))   // xls 挪进来
const isPdf     = computed(() => ext.value === 'pdf')
const isOldDoc  = computed(() => ext.value === 'doc')
const isOldPpt  = computed(() => ext.value === 'ppt')   // 新增
const isOldFormat = computed(() => isOldDoc.value || isOldPpt.value)

// Template 顺序（重要！）：
<template v-if="previewError">                          降级页（优先级最高）
<template v-else-if="isImage">                           <el-image />
<template v-else-if="isDocx && previewBuffer">           <vue-office-docx :src />
<template v-else-if="isPptx && previewBuffer">           <vue-office-docx :src />   // pptx 复用 OOXML 解析
<template v-else-if="isOldFormat">                        旧版 .doc/.ppt 不支持提示
<template v-else-if="isExcel && previewBuffer">          <vue-office-excel :src />   // xls 直接走 exceljs BIFF
<template v-else-if="isPdf && previewUrl">               <vue-office-pdf :src />
```

### 预防措施
- 任何第三方组件分支判断，**先查当前版本官方 README / NPM 包描述 + 真实样例验证**，不要凭记忆写支持列表
- 不支持的格式（.doc/.ppt）一定要单独给用户一个明确的降级提示：「旧版格式不支持，建议下载后用 WPS 另存为 .docx/.pptx，或由后端自动转换（见方案评估文档）」

---

## 7. ZIP Central Directory `_rels/.rels` 是三者通用关系文件，误归为 docx 专属导致 xlsx → detected=docx 误判（本次最终命中的致命 bug！P0）

### 问题描述（2026-08-10 10:35 Console 实锤）
用户点击 `XASL-PR-027 生产过程自检记录.xlsx`，完整链路：
```
byteLength=16922 ✅
魔数检测：{redirect:false, kind:'zip-or-ooxml'} ✅
ZIP Central Directory 子类型检测：ext=xlsx detected=docx ❌
→ previewError = '实际为 Word 文件，请确认文件扩展名与内容是否一致'
```
真实 Network Hex 100% 是 xlsx 结构（xl/workbook.xml / sheet1.xml / [Content_Types].xml 全命中）。

### 根因分析
`_detectOfficeKindFromZip()` 原来写的：
```js
if (names.has('ppt/presentation.xml')) return 'pptx'
if (names.has('word/document.xml') || names.has('_rels/.rels')) return 'docx'   // ❌ 致命错误
if (names.has('xl/workbook.xml')) return 'xlsx'                                        // ← 永远执行不到
```

**知识盲区**：`_rels/.rels` 是 **OOXML 三个子类型（docx / xlsx / pptx）共有的根关系文件**（ZIP 根目录下都有它，描述 Package 级别的关系），**绝对不能作为 docx 专属判断依据**。真实 xlsx ZIP Central Directory 里同时存在：
```
[Content_Types].xml ✅
_rels/.rels ✅            ← 先命中这行 → 返回 docx ❌
xl/workbook.xml ✅        ← 永远执行不到
```

### 解决方案
**只用各子目录专属文件判断**：
```js
if (!names.has('[content_types].xml')) return 'zip'     // 没有 [Content_Types].xml 就是普通 ZIP
if (names.has('ppt/presentation.xml') || names.has('ppt/slides/_rels/slide1.xml.rels')) return 'pptx'
if (names.has('xl/workbook.xml')        || names.has('xl/_rels/workbook.xml.rels'))         return 'xlsx'
if (names.has('word/document.xml')      || names.has('word/_rels/document.xml.rels'))       return 'docx'
return 'zip'
```

### 预防措施
- OOXML 结构判断必须严格遵守「子目录专属」原则：word/ 目录专属 → docx；xl/ 目录专属 → xlsx；ppt/ 目录专属 → pptx；根目录文件（`_rels/.rels` / `[Content_Types].xml`）一律只能当 OOXML 存在性判断，不能当子类型判断
- 写完检测函数一定拿 3 份真实的 docx / xlsx / pptx 做单元测试，打印 Central Directory 文件名集合，不要靠猜

---

## 8. .xls 是二进制 BIFF，不是 ZIP → 跳过 ZIP 魔数 + Central Directory 校验（P1）

### 问题描述
按支持矩阵 .xls 可以直接走 @vue-office-excel，但如果走 ZIP 魔数判断分支会直接失败：`_looksLikeAuthRedirect` 返回 {redirect:false}（因为 BIFF 魔数不在白名单也不在 HTML 特征里），接着 `_detectOfficeKindFromZip` 里 ZIP 魔数不匹配 → detected=null → 进入「内容不是合法 xlsx」降级。

### 解决方案
Office Layer 4 分支里先判断 `ext === 'xls'`，跳过 ZIP 结构双重校验：
```js
if (isDocx || isPptx || isExcel) {
  let detected = null
  if (ext.value !== 'xls') {
    detected = _detectOfficeKindFromZip(buf)   // xlsx/docx/pptx 正常双重校验
  } else {
    // .xls BIFF 二进制直接跳过，底层 exceljs 自己做 BIFF 解析
    console.log('[DocPreview] ext=.xls 跳过 ZIP Central Directory 检测，走 exceljs BIFF 兼容解析')
  }
  // .xls 不进子类型不一致判断（因为不是 OOXML 没有 detected）
  if (ext.value !== 'xls') { /* 扩展名 vs detected 对比 */ }
  previewBuffer.value = buf.slice(0)
}
```

### 预防措施
- OOXML 双重校验的前置条件必须加 `ext !== 'xls'`
- 将来加 .bmp / .tiff / .ppt 旧格式时同理，各自的专属魔数白名单先加上，不要统一进入 ZIP 校验分支

---

## 9. 下载按钮 window.open 缺 token → 拿到登录页 HTML 存成 xlsx（P1）

### 问题描述
预览的右上角「下载」按钮，最开始直接 `window.open(url)`，和坑 2 一样缺 Authorization Header，下载到的是登录页 HTML 保存成文件名.xlsx，用户打开 Excel 直接报「文件格式与扩展名不一致」。

### 解决方案
复用 http.js blob 下载 + createObjectURL：
```js
const download = async () => {
  const url   = buildFileUrl()
  const name  = props.file?.name || 'file'
  const blob  = await _httpGetBlob(url)        // ← token 自动注入
  const safeBlob = blob instanceof Blob ? blob : new Blob([blob])
  const a = document.createElement('a')
  a.href = URL.createObjectURL(safeBlob)
  a.download = name
  document.body.appendChild(a); a.click(); a.remove()
  setTimeout(() => URL.revokeObjectURL(a.href), 30_000)
}
```

### 预防措施
- 任何下载按钮，只要后端接口有 `[JWTAuthorize]`，一律不能直接 `window.open` / `<a href>`，必须走 http.js blob + createObjectURL 模式

---

## 10. watch immediate:deep + v-if 初始化顺序（防 future bug）（P2）

### 问题描述（潜在）
DocPreview 是 `<DocPreview v-if="currentFile">`，script setup 里 `watch(() => props.file, loadPreview, { immediate:true, deep:true })`。如果 v-if 条件为真的瞬间 props.file 是一个新对象，watch immediate 会不会和父组件 onFileSelect 赋值顺序冲突？

### 根因分析
不会冲突，但要注意：immediate 先在挂载时执行一次（props.file 有值 → loadPreview 立刻调），之后每次 deep watch 触发再调一次。如果 props.file 是个 Proxy（element-plus tree 返回的是 Proxy(Object)），deep=true 会把嵌套属性都监听，只要引用地址变了就会触发多次。

### 解决方案
- 保留 immediate:true（保证挂载瞬间就进入预览，不用等下一个 tick）
- 保留 deep:true（props.file 是 Proxy，子属性 storagePath / mimeType 被单独修改时也能触发）
- loadPreview 入口先执行 `_revoke()` 清理旧 ObjectURL，再赋值新值，防止旧 URL 泄露
- watcher 注册后立即打锚点日志：`console.log('[DocPreview] 🟢 watcher 已注册 ✅ immediate=true → loadPreview 已同步执行')`，排查初始化问题时这条日志和 🔵 script setup 初始化日志必须同时出现

### 预防措施
- 所有 v-if 控制的组件，只要在内部 watch props，**必须加锚点日志打印注册 / 触发时机**，否则永远搞不清「是 watcher 没建立，还是 props 没变」

---

## 11. vue-office 组件 4 种常见运行时错误的兜底（P1）

### 问题描述
@vue-office 组件 3 个（docx/excel/pdf）都支持 `@rendered` 和 `@error` 两个回调，但最开始没接，喂进去损坏的 OOXML 时静默失败，Console 里一条红色都没有，开发者以为是没渲染。

### 解决方案
每个 vue-office 组件都绑定 4 个属性：
```vue
<vue-office-docx
  :src="previewBuffer"
  class="docx-wrapper h-full min-h-0"
  @rendered="() => console.log('[DocPreview] rendered:', props.file?.name)"
  @error="(e) => { previewError='文档渲染失败：' + (e?.message || e); console.error('文档渲染失败', e) }"
/>
```
- 渲染成功 → 锚点日志一定出现，和 `🟢✅ 已交付给 vue-office` 日志形成「交付 → 渲染完成」闭环
- 渲染失败 → 直接赋值 previewError 显示降级页，红色错误堆栈 Console 可见

### 预防措施
- 任何第三方渲染组件（vue-office / echarts / 富文本编辑器），**必须接 @error / @rendered / onError 回调**，不能静默失败
- 回调里必须同时打 `console.error(e)`，否则不知道是组件问题还是喂进去的数据有问题

---

## 12. MinIO / 后端 StandardDirectoryFile 表字段：storagePath 实际格式确认（防 future bug）（P2）

### 问题描述（潜在）
DocPreview 最开始判断文件扩展名时用了 `props.file?.url?.split('?')[0]?.split('/').pop()?.split('.').pop()?.toLowerCase()`，但真实 props.file 没有 url 属性，只有 storagePath。

### 根因分析
StandardDirectoryFile.cs 表里（真实 DB 结构）存的就是 `StoragePath`，直接是：
```
/ISO134852016/STAGE01/FD-SDC-ISO134852016|STAGE01|L03|S005/XASL-PR-027 生产过程自检记录.xlsx
```
前端 FileTree 节点里的 file 对象里就是 `storagePath` 这个字段，没有单独的 url 属性。

### 解决方案
扩展名、文件名统一走：
```js
const fileStoragePath = computed(() => props.file?.storagePath || '')
const ext = computed(() => (fileStoragePath.value.split('/').pop()?.split('.').pop() || '').toLowerCase())
// 真实下载 URL：
return `/api/standard-directory/download?path=${encodeURIComponent(fileStoragePath.value)}`
```
**不要**从 props.file?.name 拆分扩展名（用户可能上传时改了显示名，但扩展名和真实 storagePath 一致），name 只做 display，extension 一律从 storagePath 的路径末段拆分。

### 预防措施
- 任何涉及扩展名 / mime 判断，一律以「真实存储路径的最后一段」为准，不要以显示名为准
- URL 拼接时 `path` 参数一定要 `encodeURIComponent(storagePath)`，否则 path 里有 `| / 空格 中文` 会直接被浏览器截断，400 Bad Request

---

## 附：最终正确渲染的日志链（xlsx 真实文件）

```
[FileTree] onNodeClick: {type:'file', id:'FL-FD-...XASL-PR-027 生产过程自检记录.xlsx', name:'XASL-PR-027 生产过程自检记录.xlsx'}
[FileTree] ✅ emit select → FL-FD-...XASL-PR-027 生产过程自检记录.xlsx XASL-PR-027 生产过程自检记录.xlsx
[DocExtractionRule] ✅ onFileSelect 触发: {id:'FL-FD-...', name:'XASL-PR-027 生产过程自检记录.xlsx', type:'file', storagePath:'/ISO134852016/STAGE01/...xlsx', mimeType:'xlsx'}
[DocPreview] 🔵 script setup 已初始化 ✅
[DocPreview] 🟡 开始预览 XASL-PR-027 生产过程自检记录.xlsx  URL=/api/standard-directory/download?path=%2FISO134852016%2FSTAGE01%2F...xlsx
[DocPreview] 🟢 watcher 已注册 ✅ immediate=true → loadPreview 已同步执行
[DocPreview] 二进制下载完成, byteLength=16922
[DocPreview] 魔数检测： {redirect: false, kind: 'zip-or-ooxml'}
[DocPreview] ZIP Central Directory 子类型检测：ext=xlsx detected=xlsx
[DocPreview] 🟢✅ 已交付给 vue-office：ext=xlsx byteLength=16922 kind=zip-or-ooxml
[DocPreview] rendered: XASL-PR-027 生产过程自检记录.xlsx
```

---

## 防复发检查清单（每次新增预览 / 文件下载功能前必对照）

- [ ] 所有 HTTP 请求通过 `http.js`，没有原生 fetch / 独立 axios 实例
- [ ] `http.get` 返回值直接当 data 用，没有多余 `.data` 一层（先打印 `typeof res` / `res?.byteLength` 确认）
- [ ] 不是通过 Content-Type 判断是否重定向，而是通过魔数白名单 + payload 头 256 bytes 特征判断
- [ ] 根容器填满可视区用 absolute top/left/right/bottom，不用 height:100%
- [ ] 所有 flex 嵌套子容器加 min-height:0
- [ ] 背景色有对比色，padding 视觉可见（不是只有盒模型生效）
- [ ] @vue-office 支持分支按官方支持矩阵写，顺序：docx/pptx → 旧格式提示 → xls/xlsx → pdf
- [ ] OOXML 子类型判断只用「word/ / xl/ / ppt/ 目录专属文件」，不用根目录的 _rels/.rels 和 [Content_Types].xml
- [ ] .xls 跳过 ZIP 魔数 + Central Directory 校验，直接走 exceljs BIFF
- [ ] 下载按钮通过 http.js blob + createObjectURL，不是 window.open
- [ ] watcher 初始化 + @vue-office 两个回调都有锚点日志，可追踪「props 传入 → 下载 → 交付 → 渲染成功 / 失败」全链路
- [ ] 扩展名一律从 storagePath 拆分，不要从显示名拆分
