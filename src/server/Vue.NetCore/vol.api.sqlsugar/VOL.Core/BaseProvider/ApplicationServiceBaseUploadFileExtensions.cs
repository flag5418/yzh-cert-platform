using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VOL.Core.Extensions;
using VOL.Core.Utilities;

namespace VOL.Core.BaseProvider
{
    public static class ApplicationServiceBaseUploadFileExtensions
    {
        public static string Save<TEntity>(this List<IFormFile> files, string filePath, string fileName = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = $"Upload/Tables/{typeof(TEntity).GetEntityTableName()}/{DateTime.Now.ToString("yyyMMddHHmmsss") + new Random().Next(100, 999)}/";
            }
            return files.Save(filePath, fileName);
        }
        public static string Save(this List<IFormFile> files, string filePath, string fileName = null)
        {
            if (fileName?.Trim() == "") fileName = null;
            filePath = ValidationPath(filePath) ?? "";
            string fullPath = filePath.MapPath(true);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            for (int i = 0; i < files.Count; i++)
            {
                string name = i == 0 ? (fileName ?? files[i].FileName) : files[i].FileName;
                using var stream = new FileStream(Path.Combine(fullPath, name), FileMode.Create);
                files[i].CopyTo(stream);
            }
            return ValidationPath(filePath);
        }

        public static async Task<string> SaveAsync<TEntity>(this List<IFormFile> files, string filePath, string fileName = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = $"Upload/Tables/{typeof(TEntity).GetEntityTableName()}/{DateTime.Now.ToString("yyyMMddHHmmsss") + new Random().Next(100, 999)}/";
            }
            return await files.SaveAsync(filePath, fileName);
        }
        public static async Task<string> SaveAsync(this List<IFormFile> files, string filePath, string fileName = null)
        {
            if ((files?.Count ?? 0) == 0)
                throw new ArgumentException("请上传文件");
            if (fileName?.Trim() == "") fileName = null;
            filePath = ValidationPath(filePath) ?? "";
            string fullPath = filePath.MapPath(true);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            for (int i = 0; i < files.Count; i++)
            {
                string name = i == 0 ? (fileName ?? files[i].FileName) : files[i].FileName;
                using var stream = new FileStream(Path.Combine(fullPath, name), FileMode.Create);
                await files[i].CopyToAsync(stream);
            }
            return ValidationPath(filePath);
        }

        private static string ValidationPath(string filePath)
        {
            return filePath.Replace("\\", "/");
        }

        public static (string dicPath, string fileName, WebResponseContent) ValidationImportFile<TEntity>(this List<IFormFile> files) where TEntity : class
        {
            WebResponseContent webResponse = new();
            if ((files?.Count ?? 0) == 0)
                return (null, null, webResponse.Error("请上传文件"));
            IFormFile formFile = files[0];
            string dicPath = $"Upload/{DateTime.Now.ToString("yyyMMdd")}/{typeof(TEntity).Name}/".MapPath();
            string fileName = $"{Guid.NewGuid()}_{formFile.FileName}";

            return (ValidationPath(dicPath), fileName, webResponse.OK());
        }
    }
}
