using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using wood.ViewModels;

namespace wood.Views
{
    /// <summary>
    /// Interaction logic for testTool.xaml
    /// </summary>
    public partial class testTool : Window
    {
        public testTool()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                if (this.DataContext is VM vm)
                {
                    vm.PropertyChanged += Vm_PropertyChanged;
                }
            };
        }
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not VM vm) return;

            // 1. Khi load ảnh mới
            if (e.PropertyName == nameof(VM.CurrentImage))
            {
                RenderView(vm);
            }
            // 2. Khi chạy Pipeline xong hoặc Clear
            else if (e.PropertyName == nameof(VM.CurrentResult))
            {
                RenderView(vm);
            }
        }

        private void RenderView(VM vm)
        {
            if (hWindowControl.HalconWindow == null) return;

            // Xóa sạch cửa sổ trước khi vẽ mới
            hWindowControl.HalconWindow.ClearWindow();

            // Vẽ ảnh gốc
            if (vm.CurrentImage != null && vm.CurrentImage.IsInitialized())
            {
                hWindowControl.HalconWindow.DispObj(vm.CurrentImage);

                vm.CurrentImage.GetImageSize(out int width, out int height);
                hWindowControl.HalconWindow.SetPart(0, 0, height - 1, width - 1);
            }

            // Vẽ kết quả từ Pipeline (Region / XLD) nếu có
            if (vm.CurrentResult != null && vm.CurrentResult.Success)
            {
                // Cấu hình màu sắc hiển thị kết quả (VD: Màu đỏ, hiển thị dạng viền/margin)
                hWindowControl.HalconWindow.SetColor("red");
                hWindowControl.HalconWindow.SetDraw("margin"); // hoặc "fill" nếu muốn tô kín
                hWindowControl.HalconWindow.SetLineWidth(2);

                // Giả sử trong VisionToolResult của bạn có chứa HObject/HRegion/HXLD kết quả (vd: vm.CurrentResult.OutputRegion)
                // if (vm.CurrentResult.OutputRegion != null)
                // {
                //     hWindowControl.HalconWindow.DispObj(vm.CurrentResult.OutputRegion);
                // }
            }
        }
    }
}
