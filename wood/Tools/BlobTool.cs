using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Interface;

namespace wood.Tools
{
    public class BlobTool:IVisionTool
    {
        public string Name => "Blob";
        public string Description => "Extract regions based on gray value range.";
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 255;

        public VisionToolResult Run(object input)
        {
            try
            {

                if (input is not HRegion region)
                    return new VisionToolResult(
                        false,
                        null,
                        "Input must be GrayImage.");
                
                HRegion connected = region.Connection();
                // ở đây area k phải là tham số duy nhất, còn có thể là with,height,circularity... 
                // and ở đây là nối các điều kiện với nhau 
                HRegion selected = connected.SelectShape("area", "and", Min, Max);

                VisionToolResult result = new VisionToolResult(
                    true,
                    connected,
                    "Connection completed.");
                result.DisplayObjects.Add(selected);
                return result;
            }
            catch (Exception ex)
            {
                return new VisionToolResult(
                    false,
                    null,
                    $"Error: {ex.Message}");
            }
        }
    }
}
