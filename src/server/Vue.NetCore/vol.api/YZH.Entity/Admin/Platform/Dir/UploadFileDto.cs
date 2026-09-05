using Microsoft.AspNetCore.Http;

namespace YZH.Entity.Admin.Platform.Dir
{
    public class UploadFileDto
    {
        public IFormFile File { get; set; }
        public string DirectoryCode { get; set; }
        public string RelativePath { get; set; }
    }
}
