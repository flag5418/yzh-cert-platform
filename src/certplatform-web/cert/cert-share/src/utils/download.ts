/**
 * ★ 触发浏览器保存 Blob —— **全平台唯一实现**（替代 3 份等价代码，P1.3）。
 *
 * <para>为什么必须收口：保存逻辑曾散落三处（`utils/download.ts` · `enterprise-file.ts` ·
 * `DocPreview.vue` 内联），各自是否 `appendChild` / 是否延迟 `revokeObjectURL` 不一致 ⇒
 * 出现「某些浏览器下载无反应 / 大文件被提前回收」这类只在特定环境复现的问题。</para>
 *
 * <para>本实现取更稳健的一种：先 `appendChild` 再 `click`（Firefox 要求节点在文档中），
 * `revokeObjectURL` 延迟 1s（避免下载未开始即回收）。</para>
 */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export function parseFileNameFromDisposition(disposition: string): string {
  const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
  if (match) {
    let filename = match[1]
    if (filename.startsWith('"') && filename.endsWith('"')) filename = filename.slice(1, -1)
    return decodeURIComponent(filename)
  }
  return 'download'
}

export function fileNameOf(url: string): string {
  return url.split('/').pop() || 'download'
}
