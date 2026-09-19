using Microsoft.AspNetCore.Http;

namespace CertPlatform.Shared.Entities.Dir
{
    public class UploadFileDto
    {
        public IFormFile File { get; set; }
        public string DirectoryCode { get; set; }
        public string RelativePath { get; set; }
    }
}
