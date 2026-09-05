export function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
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
