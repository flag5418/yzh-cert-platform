import { test } from 'node:test'
import assert from 'node:assert/strict'
import { traverseDirectory } from './traverse.js'

// ---- 模拟 FileSystemEntry API（与浏览器行为一致：所有回调异步触发） ----

let fileSeq = 0

function makeFileEntry(name) {
  return {
    isFile: true,
    isDirectory: false,
    name,
    file(success, error) {
      // 模拟浏览器异步回调：延迟随机毫秒数，复现 e.file 回调晚于 Promise resolve 的竞态
      setTimeout(() => {
        const f = { name, size: 100, type: '' }
        success(f)
      }, Math.floor(Math.random() * 5) + 1)
    },
  }
}

function makeDirEntry(name, children, batchSize = 2) {
  const readers = []
  return {
    isFile: false,
    isDirectory: true,
    name,
    createReader() {
      let idx = 0
      const reader = {
        readEntries(cb, errCb) {
          // 模拟 readEntries 分批返回（Chrome 每批约 100 条），全部异步
          setTimeout(() => {
            const batch = children.slice(idx, idx + batchSize)
            idx += batchSize
            if (batch.length) cb(batch)
            else cb([])
          }, 1)
        },
      }
      readers.push(reader)
      return reader
    },
  }
}

// ---- 测试用例 ----

test('递归收集所有层级的文件，并拼接相对路径', async () => {
  const tree = makeDirEntry('folder', [
    makeFileEntry('a.txt'),
    makeDirEntry('sub', [
      makeFileEntry('b.txt'),
      makeDirEntry('subsub', [makeFileEntry('c.txt')]),
    ]),
    makeFileEntry('d.txt'),
  ])

  const files = []
  await traverseDirectory(tree, 'folder/', files)

  const paths = files.map((f) => f.webkitRelativePath).sort()
  assert.deepEqual(paths, [
    'folder/a.txt',
    'folder/d.txt',
    'folder/sub/b.txt',
    'folder/sub/subsub/c.txt',
  ])
})

test('大目录分批读取时不丢文件', async () => {
  // 250 个文件，batchSize=2 → 125 批，模拟大目录多批 readEntries
  const children = Array.from({ length: 250 }, (_, i) => makeFileEntry(`f${i}.txt`))
  children.push(makeDirEntry('deep', [makeFileEntry('deep.txt')]))
  const tree = makeDirEntry('big', children)

  const files = []
  await traverseDirectory(tree, 'big/', files)

  assert.equal(files.length, 251)
  assert.ok(files.some((f) => f.webkitRelativePath === 'big/deep/deep.txt'))
})

test('多个顶层条目（文件 + 目录）', async () => {
  const fileEntry = makeFileEntry('root.txt')
  const dirEntry = makeDirEntry('dir', [makeFileEntry('inner.txt')])

  const files = []
  await traverseDirectory(dirEntry, 'dir/', files)
  assert.deepEqual(files.map((f) => f.webkitRelativePath), ['dir/inner.txt'])

  const f = await new Promise((res) => fileEntry.file(res))
  assert.equal(f.name, 'root.txt')
})

test('空目录正常结束', async () => {
  const tree = makeDirEntry('empty', [])
  const files = []
  await traverseDirectory(tree, 'empty/', files)
  assert.deepEqual(files, [])
})
