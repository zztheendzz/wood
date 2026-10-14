using wood;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Tools;

namespace wood.Interface
{
    public interface IVisionTool
    {
        string Name { get; }

        string Description { get; }

        VisionToolResult Run(object input);
    }
}
