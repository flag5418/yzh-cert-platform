namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 文件转换任务 payload（yzh_queue_task.payload 的 file_convert 结构）
    /// </summary>
    public class FileConvertPayload
    {
        public string FileCode { get; set; }
        public string FileName { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public string ConvertType { get; set; }
    }
}
