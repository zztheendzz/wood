using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Interface;
using wood.Tools;

namespace wood.Services
{
    public  class VisionPipeline
    {
        private readonly List<IVisionTool> _tools = new();

        public IReadOnlyList<IVisionTool> Tools => _tools;

        public void Add(IVisionTool tool)
        {
            _tools.Add(tool);
        }

        public void Remove(IVisionTool tool)
        {
            _tools.Remove(tool);
        }

        public void Clear()
        {
            _tools.Clear();
        }

        public VisionToolResult Run(HImage hImage)
        {
            object currentData = hImage;
            if(currentData == null) {return new VisionToolResult(
                    false,
                    null,
                    "Input image is null"
                );
            }
            foreach (var tool in _tools)
            {
                VisionToolResult result =
                    tool.Run(currentData);

                if (!result.Success)
                    return result;

                currentData = result.Data;
            }

            return new VisionToolResult(
                true,
                currentData,
                "Pipeline completed"
            );
        }

    }
}
