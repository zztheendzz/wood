using HalconDotNet;
using System;
using wood.Interface;

namespace wood.Tools
{
    public class ToGrayTool : IVisionTool
    {
        public string Name => "To Gray";

        public string Description => "Convert RGB Image to Grayscale Image";

        public HImage? ConvertToGray(HImage inputImage)
        {
            if (inputImage == null || !inputImage.IsInitialized())
                return null;
            int channels=0;
            // Kiểm tra số kênh màu của ảnh
            //inputImage.CountChannels(out  channels);

            // Nếu ảnh đã là ảnh xám (1 kênh), giữ nguyên
            if (channels == 1)
            {
                return inputImage.Clone();
            }

            // Chuyển đổi từ RGB sang xám
            HOperatorSet.Rgb1ToGray(inputImage, out HObject grayObj);
            return new HImage(grayObj);
        }

        public VisionToolResult Run(object input)
        {
            try
            {
                if (input is not HImage image || !image.IsInitialized())
                {
                    return new VisionToolResult(
                        false,
                        null,
                        "Input is not a valid HImage.");
                }

                // 1. Chuyển đổi ảnh màu thành ảnh xám
                HImage grayImage = ConvertToGray(image)!;

                // 2. Tạo kết quả trả về (Trừ trường hợp VisionToolResult nhận HImage làm Output Data)
                VisionToolResult result = new VisionToolResult(
                    true,
                    grayImage,
                    "Converted to Grayscale successfully.");

                // 3. Đưa HImage vào danh sách hiển thị
                result.DisplayObjects.Add(grayImage);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return new VisionToolResult(
                    false,
                    null,
                    $"ToGray error 1: {ex.Message}");
            }
        }
    }
}