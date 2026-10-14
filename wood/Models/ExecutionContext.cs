using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wood.Models
{
    public class ExecutionContext
    {
        private readonly Dictionary<Guid, object?> _data = new();

        public void Set(Guid portId, object? data)
        {
            _data[portId] = data;
        }

        public object? Get(Guid portId)
        {
            _data.TryGetValue(portId, out var data);
            return data;
        }
        public bool HasValue(Guid portId)
        {
            return _data.ContainsKey(portId);
        }
    }
}
