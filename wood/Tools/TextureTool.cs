using HalconDotNet;
using System;
using wood.Interface;
using wood.Models;

namespace wood.Tools
{
    public class TextureTool : IVisionTool
    {
        public string Name => "TextureTool";

        public string Description => "Texture analysis tool";

        public TextureTool() { }

        public VisionToolResult Run(object input)
        {
            // Hàm Run mặc định (nếu bạn vẫn muốn giữ)
            return new VisionToolResult(false, null, "Hãy gọi Run2 với đầy đủ tham số cấu hình.");
        }

        private bool CompareValue(double value, double thresh, string op)
        {
            return op == ">" ? value > thresh : value < thresh;
        }

        public TextureResult Run2(
            object input,
            double threshGrayMean = 200.0, bool useGrayMean = true, string opGrayMean = ">",
            double threshGrayDev = 15.0, bool useGrayDev = true, string opGrayDev = "<",
            double threshSatMean = 15.0, bool useSatMean = true, string opSatMean = "<",
            double threshSatDev = 10.0, bool useSatDev = true, string opSatDev = "<",
            double threshTextureMean = 30.0, bool useTextureMean = true, string opTextureMean = "<")
        {
            HObject domain = null, texture = null, r = null, g = null, b = null, h = null, s = null, v = null, gray = null;

            try
            {
                HImage? image = input as HImage;
                if (image == null) return new TextureResult { Merterial = "Không có ảnh đầu vào" };

                HOperatorSet.GetDomain(image, out domain);
                HOperatorSet.CountChannels(image, out HTuple channels);
                bool isColorImage = (channels.I == 3);

                // Texture Laws
                HOperatorSet.TextureLaws(image, out texture, "el", 7, 3);
                HOperatorSet.Intensity(domain, texture, out HTuple meanTexture, out HTuple devTexture);
                double textureMean = meanTexture.D;
                double textureDeviation = devTexture.D;

                // Saturation
                double saturationMean = 0, saturationDeviation = 0;
                if (isColorImage)
                {
                    HOperatorSet.Decompose3(image, out r, out g, out b);
                    HOperatorSet.TransFromRgb(r, g, b, out h, out s, out v, "hsv");
                    HOperatorSet.Intensity(domain, s, out HTuple meanS, out HTuple devS);
                    saturationMean = meanS.D;
                    saturationDeviation = devS.D;
                }

                // Gray
                if (isColorImage)
                {
                    HOperatorSet.Rgb1ToGray(image, out gray);
                }
                else
                {
                    gray = image;
                }

                HOperatorSet.Intensity(domain, gray, out HTuple meanGray, out HTuple devGray);
                double grayMean = meanGray.D;
                double grayDeviation = devGray.D;

                // TÍNH TOÁN ĐIỀU KIỆN ĐỘNG LINH HOẠT
                bool condGrayMean = !useGrayMean || CompareValue(grayMean, threshGrayMean, opGrayMean);
                bool condGrayDev = !useGrayDev || CompareValue(grayDeviation, threshGrayDev, opGrayDev);
                bool condSatMean = !useSatMean || CompareValue(saturationMean, threshSatMean, opSatMean);
                bool condSatDev = !useSatDev || CompareValue(saturationDeviation, threshSatDev, opSatDev);
                bool condTextureMean = !useTextureMean || CompareValue(textureMean, threshTextureMean, opTextureMean);

                bool isXop = isColorImage
                    ? (condGrayMean && condGrayDev && condSatMean && condSatDev && condTextureMean)
                    : (condGrayMean && condGrayDev && condTextureMean);

                string materialResult = isXop ? "XỐP" : "GỖ";

                return new TextureResult
                {
                    TextureMean = textureMean,
                    TextureDeviation = textureDeviation,
                    SaturationMean = saturationMean,
                    SaturationDeviation = saturationDeviation,
                    GrayMean = grayMean,
                    GrayDeviation = grayDeviation,
                    Merterial = materialResult
                };
            }
            catch (Exception)
            {
                return new TextureResult { Merterial = "Lỗi thuật toán" };
            }
            finally
            {
                domain?.Dispose();
                texture?.Dispose();
                r?.Dispose();
                g?.Dispose();
                b?.Dispose();
                h?.Dispose();
                s?.Dispose();
                v?.Dispose();
                if (gray != input) gray?.Dispose();
            }
        }
    }
}