using System;
using System.Collections.Generic;
using System.Text;

namespace YZH.Core.DBManager
{
    public class DBConnectionAttribute : Attribute
    {
        public string DBName { get; set; }
    }
}
