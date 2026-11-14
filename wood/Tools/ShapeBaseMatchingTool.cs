using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using wood.Interface;

namespace wood.Tools
{
    public class ShapeBaseMatchingTool : IVisionTool
    {
        public string Name => "ShapeBaseMatchingTool";
        public string Description => "Shape-Base Matching Tool";

        // Khai báo các biến HALCON cần thiết
        HObject ho_Image = null, ho_ModelRegion = null, ho_ImageReduced = null;
        HObject ho_Contours = null;
        HTuple hv_ModelID = new HTuple();
        HTuple hv_Row = new HTuple(), hv_Column = new HTuple(), hv_Angle = new HTuple(), hv_Score = new HTuple();

        // --- BỔ SUNG: Lưu lại tọa độ chuẩn (Golden Pose) để tính độ lệch ---
        private double referenceX = 0;
        private double referenceY = 0;
        private double referenceAngle = 0;
        private bool isReferenceInitialized = false;

        public VisionToolResult Run(object input)
        {
            throw new NotImplementedException();
        }

        public bool FindObjectAndGetPose(HObject currentImage, out int worldX, out int worldY, out double angleDeg, out double deltaX, out double deltaY, out double deltaAngle)
        {
            worldX = 0;
            worldY = 0;
            angleDeg = 0;
            deltaX = 0;
            deltaY = 0;
            deltaAngle = 0;

            try
            {
                // Kiểm tra xem model đã được tạo hay load chưa
                if (hv_ModelID == null || hv_ModelID.Length == 0)
                {
                    // Thử load model từ file nếu có lưu sẵn trước đó
                    string modelPath = "shape_model.shm";
                    if (File.Exists(modelPath))
                    {
                        HOperatorSet.ReadShapeModel(modelPath, out hv_ModelID);
                    }
                    else
                    {
                        return false; // Chưa có model để so sánh!
                    }
                }

                // 1. Thực hiện tìm kiếm Shape Model trên ảnh hiện tại
                HOperatorSet.FindShapeModel(
                    currentImage,
                    hv_ModelID,
                    (new HTuple(-180)).TupleRad(),
                    (new HTuple(360)).TupleRad(),
                    0.75,
                    1,
                    0.5,
                    "least_squares",
                    0,
                    0.8,
                    out hv_Row,
                    out hv_Column,
                    out hv_Angle,
                    out hv_Score
                );

                // 2. Kiểm tra xem có tìm thấy đối tượng nào không (Score > 0.75)
                if (hv_Score.Length > 0 && hv_Score.D > 0.75)
                {
                    double pixelRow = hv_Row.D;
                    double pixelCol = hv_Column.D;
                    double angleRad = hv_Angle.D;

                    // FIX LỖI CÚ PHÁP: Chuyển đổi góc từ Radian sang Độ đúng chuẩn C#
                    angleDeg = angleRad * (180.0 / Math.PI);

                    // 3. Quy đổi từ tọa độ ảnh sang tọa độ thực tế (World X, Y)
                    ConvertPixelToWorld(pixelRow, pixelCol, out worldX, out worldY);

                    // --- TÍNH TOÁN ĐỘ LỆCH SO VỚI MẪU CHUẨN ---
                    if (isReferenceInitialized)
                    {
                        deltaX = worldX - referenceX;
                        deltaY = worldY - referenceY;
                        deltaAngle = angleDeg - referenceAngle;
                    }
                    else
                    {
                        // Nếu chưa set mẫu chuẩn, lấy lần đầu tiên làm chuẩn
                        referenceX = worldX;
                        referenceY = worldY;
                        referenceAngle = angleDeg;
                        isReferenceInitialized = true;
                    }

                    return true; // Tìm thấy và so sánh thành công
                }
            }
            catch (HalconException ex)
            {
                // Xử lý ngoại lệ nếu có lỗi thực thi HALCON
            }

            return false; // Không tìm thấy vật thể
        }

        private void ConvertPixelToWorld(double r, double c, out int x, out int y)
        {
            x = (int)c;
            y = (int)r;
        }

        public void TrainShapeModel(HObject inputImage, HRegion templateRegion)
        {
            try
            {
                // 1. Cắt vùng cần học từ ảnh mẫu
                HOperatorSet.ReduceDomain(inputImage, templateRegion, out ho_ImageReduced);

                // 2. Tạo Shape Model và lưu vào biến hv_ModelID
                HOperatorSet.CreateShapeModel(
                    ho_ImageReduced,
                    "auto",
                    (new HTuple(-180)).TupleRad(),
                    (new HTuple(360)).TupleRad(),
                    "auto",
                    "auto",
                    "use_polarity",
                    "auto",
                    "auto",
                    out hv_ModelID
                );

                // GỢI Ý: Nên lưu Model ra file để lần sau bật phần mềm lên không cần Train lại
                HOperatorSet.WriteShapeModel(hv_ModelID, "shape_model.shm");

                // 3. Lấy các contour biên để hiển thị
                HOperatorSet.GetShapeModelContours(out ho_Contours, hv_ModelID, 1);

                System.Windows.Forms.MessageBox.Show("Tạo và lưu Shape Model thành công!");
            }
            catch (HalconException ex)
            {
                System.Windows.Forms.MessageBox.Show("Lỗi tạo Model: " + ex.GetErrorMessage());
            }
        }
    }
}