import { describe, it, expect } from 'vitest'
import {
  checkUploadable,
  partitionUploadable,
  isSystemNoise,
  extOf,
} from '@share/constants/upload-file-policy'

describe('R21 验收用例（手工用例固化）', () => {
  it('a.exe 拒（never 黑名单）', () => {
    const v = checkUploadable('a.exe')
    expect(v.ok).toBe(false)
    expect(v.reason).toBe('bad_extension')
  })
  it('.DS_Store 静默（system_noise，分区后不进 rejected）', () => {
    expect(isSystemNoise('.DS_Store')).toBe(true)
    expect(checkUploadable('.DS_Store').reason).toBe('system_noise')
    const part = partitionUploadable([{ name: '.DS_Store' }, { name: '合同.docx' }])
    expect(part.noise.map((x) => x.name)).toEqual(['.DS_Store'])
    expect(part.rejected).toEqual([])
    expect(part.accepted.map((x) => x.name)).toEqual(['合同.docx'])
  })
  it('evil.html 拒（script 黑名单）', () => {
    const v = checkUploadable('evil.html')
    expect(v.ok).toBe(false)
    expect(v.reason).toBe('bad_extension')
  })
  it('照片.webp 过（image 白名单）', () => {
    expect(checkUploadable('照片.webp').ok).toBe(true)
    expect(extOf('照片.WEBP')).toBe('.webp')
  })
  it('压缩包默认不开口（archive ⊄ DEFAULT_UPLOAD_CATEGORIES）', () => {
    expect(checkUploadable('bundle.zip').ok).toBe(false)
  })
})
