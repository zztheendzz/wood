using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Interface;

namespace wood.Tools
{
    public class ThresholdTool : IVisionTool
    {
        public string Name => "Threshold";

        public string Description =>
            "Extract regions based on gray value range.";

        public double Min { get; set; } =50;

        public double Max { get; set; } = 155;



        public VisionToolResult Run(object input)
        {
            try
            {
                if (input is not HImage image)
                {
                    return new VisionToolResult(
                        false,
                        null,
                        "Input must be HImage.");
                }

                // Threshold
                HRegion region =
                    image.Threshold(Min, Max);

                // Tạo kết quả
                VisionToolResult result =
                    new VisionToolResult(
                        true,
                        region,
                        $"Threshold: {Min} - {Max}");

                // Object cần hiển thị
                result.DisplayObjects.Add(region);

                return result;
            }
            catch (Exception ex)
            {
                return new VisionToolResult(
                    false,
                    null,
                    ex.Message);
            }
        }
    }
    
}
