using System;
using System.Collections.Generic;
using System.Text;

namespace VOL.Core.Enums
{
    [Flags]
    public enum ActionPermissionOptions
    {
        //注意添加的枚举值一定要是前面的值倍数，即x2
        Add = 1,
        Update = 2,
        Search = 4,
        Export = 8,
        Delete = 16,
        Audit = 32,
        Upload = 64,//上传文件
        Import = 128//导入表数据Excel
    }
}
