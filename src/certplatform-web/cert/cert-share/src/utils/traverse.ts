/**
 * 从 FileEntry 读取 File 对象（Promise 化）
 */
export const getFileFromEntry = (fileEntry: FileSystemFileEntry): Promise<File | null> => {
  return new Promise((resolve) => {
    fileEntry.file((f) => resolve(f), () => resolve(null))
  })
}

/**
 * 递归遍历文件夹
 *
 * 注意：
 * 1. 大目录下 readEntries 会分多批返回（Chrome 每批约 100 条），必须循环读到空数组；
 * 2. 必须 await 每个 e.file() 回调，否则 Promise 提前 resolve 后，
 *    才被推入 fileList 的深层文件会随局部数组一起被丢弃（表现为只能拿到第一层文件）。
 *
 * @param entry 目录条目
 * @param path 当前路径前缀（如 'folder/sub/'）
 * @param fileList 收集文件的目标数组
 * @returns Promise<void>
 */
export const traverseDirectory = (
  entry: FileSystemDirectoryEntry,
  path: string,
  fileList: File[]
): Promise<void> => {
  return new Promise((resolve) => {
    const reader = entry.createReader()
    const allEntries: FileSystemEntry[] = []

    const readBatch = () => {
      reader.readEntries(
        (batch) => {
          if (!batch.length) {
            processEntries(allEntries).then(resolve)
            return
          }
          allEntries.push(...batch)
          readBatch()
        },
        () => {
          processEntries(allEntries).then(resolve)
        }
      )
    }

    const processEntries = async (list: FileSystemEntry[]) => {
      for (const e of list) {
        if (e.isFile) {
          const file = await getFileFromEntry(e as FileSystemFileEntry)
          if (file) {
            if (path) {
              Object.defineProperty(file, 'webkitRelativePath', {
                value: path + file.name,
                writable: false,
              })
            }
            fileList.push(file)
          }
        } else if (e.isDirectory) {
          await traverseDirectory(e as FileSystemDirectoryEntry, path + e.name + '/', fileList)
        }
      }
    }

    readBatch()
  })
}
