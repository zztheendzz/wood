using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using wood.Interface;
using wood.Models;

namespace wood.Tools
{
    public class Region : IVisionTool
    {
        public string Name => "Region";
        public string Description => "Select region in origin image";
        public  HRegion RegionSelected { get;  set; } = new HRegion();
        //public HGenericShapeModelResult Shape { get; set; }

        public HTuple row1;
        public HTuple column1;
        public HTuple row2;
        public HTuple column2;

        private HDrawingObject _drawObj;
        private HWindow _window;
        public void BeginDraw(HWindow window, double row1 = 100, double col1 = 100,
                                      double row2 = 300, double col2 = 300)
        {
            System.Diagnostics.Debug.WriteLine("BeginDraw called");
            EndDraw();   // dọn khung cũ nếu có
            _window = window;

            _drawObj = HDrawingObject.CreateDrawingObject(
                HDrawingObject.HDrawingObjectType.RECTANGLE1,
                row1, col1, row2, col2);

            _window.AttachDrawingObjectToWindow(_drawObj);
        }
        public void EndDraw()
        {
            if (_drawObj != null)
            {
                _window?.DetachDrawingObjectFromWindow(_drawObj);
                _drawObj.Dispose();
                _drawObj = null;
            }
        }
        public VisionToolResult Run(object input)
        {
            if (_drawObj == null)
                return new VisionToolResult(false, null, "Chưa vẽ vùng");

            double r1 = _drawObj.GetDrawingObjectParams("row1").D;
            double c1 = _drawObj.GetDrawingObjectParams("column1").D;
            double r2 = _drawObj.GetDrawingObjectParams("row2").D;
            double c2 = _drawObj.GetDrawingObjectParams("column2").D;
            System.Diagnostics.Debug.WriteLine("r1"+ r1);
            System.Diagnostics.Debug.WriteLine("c1" + c1);
            System.Diagnostics.Debug.WriteLine("r2" + r2);
            System.Diagnostics.Debug.WriteLine("c2" + c2);
            RegionSelected?.Dispose();
            RegionSelected = new HRegion();
            RegionSelected.GenRectangle1(r1, c1, r2, c2);

            EndDraw();

            return new VisionToolResult(true, new RegionResult
            {
                Row1 = r1,
                Column1 = c1,
                Row2 = r2,
                Column2 = c2,
                Region = RegionSelected
            }, "select rectangle");
        }


    }
}
