using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Enum;

namespace wood.Models
{
    public class Ports
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Tên Port: Image, Region, Result...
        public string Name { get; set; } = string.Empty;

        // Input hoặc Output
        public PortDirection Direction { get; set; }

        // Kiểu dữ liệu mà Port nhận/trả
        public DataType DataType { get; set; }

        // ID của Node sở hữu Port này
        public Guid NodeId { get; set; }
        public Ports() { }
    }
}
