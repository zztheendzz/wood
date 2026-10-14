using HalconDotNet;
using System;
using System.ComponentModel;
using System.Windows;
using wood.Models;
using wood.Tools;
using wood.ViewModels;

namespace wood.Views
{
    public partial class ViewHalcon : Window
    {
        private readonly Region _regionTool = new Region();

        // Biến lưu kích thước ảnh trước đó để tránh gọi SetPart vô tận khi Live Stream
        private int _lastLiveWidth = 0;
        private int _lastLiveHeight = 0;

        public ViewHalcon()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is VM vm)
                {
                    vm.PropertyChanged += Vm_PropertyChanged;
                }
            };
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not VM vm) return;

            // 1. Luồng LIVE STREAM: Cập nhật liên tục lên hWindowControlLive
            if (e.PropertyName == nameof(VM.LiveImage))
            {
                Dispatcher.Invoke(() => RenderLiveView(vm));
            }
            // 2. Luồng TRIGGER INSPECTION: Chỉ cập nhật khi chụp ảnh/kích hoạt kiểm tra lên hWindowControl
            else if (e.PropertyName == nameof(VM.CurrentImage))
            {
                Dispatcher.Invoke(() => RenderTriggerView(vm));
            }
            // 3. Cập nhật ROI / Kết quả kiểm tra lên hWindowControl
            else if (e.PropertyName == nameof(VM.CurrentResult) || e.PropertyName == nameof(VM.CurrentRegion))
            {
                Dispatcher.Invoke(() => RenderTriggerView(vm));
            }
        }

        /// <summary>
        /// Hiển thị luồng Live Camera liên tục ở Tab Connection (HLive)
        /// </summary>
        private void RenderLiveView(VM vm)
        {
            if (vm.LiveImage == null || !vm.LiveImage.IsInitialized()) return;
            if (hWindowControlLive?.HalconWindow == null) return;

            HWindow winLive = hWindowControlLive.HalconWindow;

            vm.LiveImage.GetImageSize(out int width, out int height);

            // Chỉ đặt lại viewport khi kích thước khung hình thay đổi (giúp giữ được thao tác Zoom/Pan của người dùng)
            if (_lastLiveWidth != width || _lastLiveHeight != height)
            {
                winLive.SetPart(0, 0, height - 1, width - 1);
                _lastLiveWidth = width;
                _lastLiveHeight = height;
            }

            winLive.ClearWindow();
            winLive.DispObj(vm.LiveImage);
        }

        /// <summary>
        /// Hiển thị ảnh Trigger + ROI + Kết quả ở Tab Main Inspection (HControl)
        /// </summary>
        private void RenderTriggerView(VM vm)
        {
            if (vm.CurrentImage == null || !vm.CurrentImage.IsInitialized()) return;
            if (hWindowControl?.HalconWindow == null) return;

            HWindow winMain = hWindowControl.HalconWindow;

            winMain.ClearWindow();
            vm.CurrentImage.GetImageSize(out int width, out int height);

            // Cập nhật lại khung nhìn vừa vặn với ảnh Trigger vừa chụp
            winMain.SetPart(0, 0, height - 1, width - 1);
            winMain.DispObj(vm.CurrentImage);

            // Vẽ ROI/Region kiểm tra lên trên ảnh nếu có
            if (vm.CurrentRegion?.Region != null && vm.CurrentRegion.Region.IsInitialized())
            {
                winMain.SetColor("green");
                winMain.SetDraw("margin");
                winMain.SetLineWidth(2);
                winMain.DispObj(vm.CurrentRegion.Region);
            }
        }

        #region Region & Event Handlers

        private void BtnRegion_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VM vm) return;

            if (vm.CurrentImage == null || !vm.CurrentImage.IsInitialized())
            {
                MessageBox.Show("Chưa có ảnh Trigger để vẽ Region!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HWindow window = hWindowControl.HalconWindow;
            vm.CurrentImage.GetImageSize(out int w, out int h);

            window.ClearWindow();
            window.DispObj(vm.CurrentImage);

            try
            {
                // Sửa lỗi toán tử nhân h * 0.25, w * 0.25
                _regionTool.BeginDraw(window, h * 0.25, w * 0.25, h * 0.75, w * 0.75);
            }
            catch (HOperatorException ex)
            {
                MessageBox.Show($"Lỗi thao tác HALCON: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFinishRegion_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VM vm) return;
            HWindow window = hWindowControl.HalconWindow;

            VisionToolResult result = _regionTool.Run(window);
            if (!result.Success || result.Data == null) return;

            var regionResult = (RegionResult)result.Data;
            vm.CurrentRegion = regionResult;

            window.SetColor("green");
            window.SetDraw("margin");
            window.SetLineWidth(2);
            window.DispObj(regionResult.Region);
        }

        private void BtnTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VM vm) return;

            // Gọi Command Trigger ở ViewModel để lấy ảnh từ Camera
            if (vm.TriggerCommand.CanExecute(null))
            {
                vm.TriggerCommand.Execute(null);
            }
        }

        #endregion
    }
}