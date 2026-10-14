using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wood.Tools
{
    public class VisionToolResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public object? Data { get; set; }
        public Dictionary<string, string> Values { get; set; } = new();
        //cần quản lý Dispose 
        public List<HObject> DisplayObjects { get; set; } = new();

        public VisionToolResult()
        {
        }

        public VisionToolResult(
            bool success,
            object? data,
            string message = "")
        {
            Success = success;
            Data = data;
            Message = message;
        }
    }
}
