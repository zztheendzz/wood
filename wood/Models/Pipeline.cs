using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wood.Models
{
    public class Pipeline
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public List<Nodes> Nodes { get; set; } = new();

        public List<Connection> Connections { get; set; } = new();
    }
}
