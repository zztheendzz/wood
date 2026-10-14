using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Nodify;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using wood.Models;
using wood.Services;
using wood.Services.App;
using wood.Services.Communication.Camera;
using wood.Services.Communication.Com;
using wood.Tools;
using wood.Views.pages;

namespace wood.ViewModels
{
    public partial class VM : ObservableObject
    {
        // 1. Ảnh hiển thị chính ở tab Main Inspection (chỉ cập nhật khi Trigger/Load file)
        [ObservableProperty]
        private HImage? currentImage;

        // 2. Ảnh hiển thị luồng Live Stream ở tab Connection
        [ObservableProperty]
        private HImage? liveImage;

        [ObservableProperty]
        private string imagePath = string.Empty;

        private readonly VisionPipeline _pipeline;

        [ObservableProperty] private VisionToolResult? currentResult;

        [ObservableProperty] private string statusMessage = "Ready";

        [ObservableProperty]
        private RegionResult? currentRegion;

        [ObservableProperty]
        private TextureResult? textureResult;

        public IRelayCommand OpenConnectionCommand { get; }

        public TcpClientService tcpService = new TcpClientService();

        public Camera camera = new Camera();
        private readonly Com _comService;

        #region ================= PROPERTIES FOR CONNECTION PAGE =================

        // --- Cấu hình COM Port ---
        [ObservableProperty]
        private ObservableCollection<string> comPortList = new();

        [ObservableProperty]
        private string selectedComPort = "COM1";

        [ObservableProperty]
        private int selectedBaudRate = 9600;

        [ObservableProperty]
        private string textToSendCom = string.Empty;

        [ObservableProperty]
        private string receivedComData = string.Empty;

        [ObservableProperty]
        private string receivedComDataAll = string.Empty;

        [ObservableProperty]
        private bool isComConnected;

        // --- Cấu hình IP Camera ---
        [ObservableProperty]
        private string cameraIpUrl = "rtsp://192.168.1.100:554/stream1";

        [ObservableProperty]
        private string cameraPort = "554";

        [ObservableProperty]
        private string cameraUser = "admin";

        [ObservableProperty]
        private string cameraPassword = string.Empty;

        [ObservableProperty]
        private bool isCameraStreaming;

        [ObservableProperty]
        private ObservableCollection<string> cameraList = new();

        [ObservableProperty]
        private string selectedCameraDevice = string.Empty;

        // --- Thông số kết quả thu được ---
        [ObservableProperty] public double textureMean;
        [ObservableProperty] public double textureDeviation;
        [ObservableProperty] public double saturationMean;
        [ObservableProperty] public double saturationDeviation;
        [ObservableProperty] public double grayMean;
        [ObservableProperty] public double grayDeviation;
        [ObservableProperty] public string merterial = string.Empty;

        // --- CẤU HÌNH NGƯỠNG PHÂN BIỆT (THRESHOLD SETTINGS) ---
        [ObservableProperty] public double threshGrayMean = 200.0;
        [ObservableProperty] public double threshGrayDev = 15.0;
        [ObservableProperty] public double threshSatMean = 15.0;
        [ObservableProperty] public double threshSatDev = 10.0;
        [ObservableProperty] public double threshTextureMean = 30.0;

        // --- CÁC CỜ BẬT/TẮT ĐIỀU KIỆN (Mặc định bật hết) ---
        [ObservableProperty] private bool useGrayMean = true;
        [ObservableProperty] private bool useGrayDev = true;
        [ObservableProperty] private bool useSatMean = true;
        [ObservableProperty] private bool useSatDev = true;
        [ObservableProperty] private bool useTextureMean = true;

        [ObservableProperty] private string operatorGrayMean = ">";
        [ObservableProperty] private string operatorGrayDev = "<";
        [ObservableProperty] private string operatorSatMean = "<";
        [ObservableProperty] private string operatorSatDev = "<";
        [ObservableProperty] private string operatorTextureMean = "<";

        [ObservableProperty] private string comTriggerSignal = "1";
        [ObservableProperty] private string comResponseXop = "4";
        [ObservableProperty] private string comResponseGo = "6";
        [ObservableProperty] private string comResponseError = "0";

        // --- MÁY QUÉT QR ---
        // true: mọi mã quét được đều trigger camera; false: chỉ trigger khi mã == ComTriggerSignal
        [ObservableProperty] private bool triggerOnAnyScan = true;

        // Mã QR quét gần nhất (hiển thị lên UI)
        [ObservableProperty] private string lastQrCode = string.Empty;

        // --- KẾT QUẢ THEO PALLET (mỗi pallet nhiều hàng, mỗi hàng quét 1 lần) ---
        [ObservableProperty] private string currentPalletCode = string.Empty;
        [ObservableProperty] private int palletRowCount;
        [ObservableProperty] private int palletFoamCount;
        [ObservableProperty] private int palletWoodCount;
        [ObservableProperty] private int palletErrorCount;
        [ObservableProperty] private string palletMaterial = string.Empty;

        // Quy tắc kết luận cả pallet từ các hàng:
        //   "AnyWood"  : có ít nhất 1 hàng GỖ -> pallet GỖ, ngược lại XỐP
        //   "AnyFoam"  : có ít nhất 1 hàng XỐP -> pallet XỐP, ngược lại GỖ
        //   "Majority" : theo số hàng nhiều hơn (bằng nhau -> LẪN)
        [ObservableProperty] private string palletRule = "AnyWood";
        public string[] PalletRuleOptions { get; } = { "AnyWood", "AnyFoam", "Majority" };

        // --- CẤU HÌNH LƯU ẢNH ---
        [ObservableProperty] private string saveFolderPath = @"D:\Images\Processed";

        // --- THÔNG SỐ ĐIỀU CHỈNH CAMERA (theo dải thật của camera) ---
        // ExposureTime: 0..300000 µs (double)
        [ObservableProperty]
        private double _camExposure = 5000;

        // Gain: 1..8 (double)
        [ObservableProperty]
        private double _camGain = 4.0;

        // FrameRate (double)
        [ObservableProperty]
        private double _camFrameRate = 30.0;

        // Gamma: 0..255 (int, giá trị thô của camera)
        [ObservableProperty]
        private double _camGamma = 80;

        // Contrast: 0..200 (int, giá trị thô của camera)
        [ObservableProperty]
        private double _camContrast = 100;

        public event Action<HImage, bool>? ImageUpdated;

        // volatile vì được đọc ở thread grab, ghi ở thread khác
        private volatile bool _isTriggerPending = false;

        // Trạng thái TriggerMode hiện tại của camera (null = chưa biết)
        private bool? _triggerModeOn = null;

        // Tuần tự hóa các thao tác kết nối / trigger trên camera
        private readonly object _camOpLock = new object();

        private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appconfig.json");

        // Ký tự kết thúc gói tin nhận từ COM (hiển thị dạng "\\r\\n")
        [ObservableProperty] private bool useReceiveEndChar = true;
        [ObservableProperty] private string receiveEndChar = "\\r\\n";

        private string _comReceiveBuffer = string.Empty;
        private readonly object _comBufferLock = new object();

        // --- CẤU HÌNH CHẾ ĐỘ AUTO TIMER ---
        [ObservableProperty]
        private bool isAutoTimerRunning = false;

        [ObservableProperty]
        private double autoTimerIntervalSeconds = 2.0; // Mặc định 2 giây chụp 1 lần

        private System.Threading.Timer? _autoTimer;

        // --- Cờ cho Barcode Mode ---
        [ObservableProperty]
        private bool isBarcodeModeActive = false;

        #endregion

        public VM()
        {
            _comService = new Com();
            OpenConnectionCommand = new RelayCommand(ExecuteOpenConnection);
            _pipeline = new VisionPipeline();
            _pipeline.Add(new TextureTool());

            // Đăng ký sự kiện nhận dữ liệu từ COM
            _comService.DataReceived += OnComDataReceived;

            camera.OnImageGrabbed += OnCameraImageGrabbed;
            camera.OnError += (s, err) => StatusMessage = err;

            RefreshComPorts();

            // Tự nạp cấu hình khi chạy chương trình; chưa có file thì tự tạo từ giá trị mặc định trong code
            InitConfig();

            // Xử lý lần lượt từng lần quét (1 kiện có nhiều hàng -> quét nhiều lần)
            _ = Task.Run(ScanWorkerAsync);
        }

        // ================= XỬ LÝ THAY ĐỔI THÔNG SỐ CAMERA TỪ UI =================
        // Debounce: kéo slider sinh rất nhiều lần thay đổi, chỉ gửi giá trị cuối
        // sau 150 ms không đổi, tránh dồn lệnh set lên camera.

        private readonly Dictionary<string, CancellationTokenSource> _debounceMap = new();

        [RelayCommand]
        public void ToggleAutoTimer()
        {
            IsAutoTimerRunning = !IsAutoTimerRunning;
            if (IsAutoTimerRunning)
            {
                int intervalMs = (int)(AutoTimerIntervalSeconds * 1000);
                if (intervalMs < 200) intervalMs = 200; // Bảo vệ tối thiểu 200ms

                _autoTimer = new System.Threading.Timer(async _ =>
                {
                    // Tự động gọi lại luồng Trigger
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await Trigger();
                    });
                }, null, 0, intervalMs);

                StatusMessage = $"Đã bật chế độ tự động chụp mỗi {AutoTimerIntervalSeconds} giây.";
            }
            else
            {
                _autoTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _autoTimer?.Dispose();
                _autoTimer = null;
                StatusMessage = "Đã tắt chế độ tự động chụp theo chu kỳ.";
            }
        }

        private void ApplyCameraParamDebounced(string key, Action apply, int delayMs = 150)
        {
            CancellationTokenSource cts;
            lock (_debounceMap)
            {
                if (_debounceMap.TryGetValue(key, out var old)) old.Cancel();
                cts = new CancellationTokenSource();
                _debounceMap[key] = cts;
            }

            var token = cts.Token;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    if (!token.IsCancellationRequested) apply();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    StatusMessage = $"Lỗi chỉnh {key}: {ex.Message}";
                }
            });
        }

        partial void OnCamExposureChanged(double value)
        {
            if (camera.IsConnected)
                ApplyCameraParamDebounced("Exposure", () => camera.SetExposureTime(value));
        }

        partial void OnCamGainChanged(double value)
        {
            if (camera.IsConnected)
                ApplyCameraParamDebounced("Gain", () => camera.SetGain(value));
        }

        partial void OnCamFrameRateChanged(double value)
        {
            if (camera.IsConnected)
                ApplyCameraParamDebounced("FrameRate", () => camera.SetFrameRate(value));
        }

        partial void OnCamGammaChanged(double value)
        {
            if (camera.IsConnected)
                ApplyCameraParamDebounced("Gamma", () => camera.SetGamma((int)Math.Round(value)));
        }

        partial void OnCamContrastChanged(double value)
        {
            if (camera.IsConnected)
                ApplyCameraParamDebounced("Contrast", () => camera.SetContrast((int)Math.Round(value)));
        }

        /// <summary>
        /// Đẩy các giá trị đang hiển thị trên UI xuống camera (gọi 1 lần sau khi Connect).
        /// </summary>
        private void ApplyCameraSettings()
        {
            camera.SetExposureTime(CamExposure);
            camera.SetGain(CamGain);
            camera.SetGamma((int)Math.Round(CamGamma));
            camera.SetContrast((int)Math.Round(CamContrast));
        }

        // ================= CAMERA HELPERS =================

        /// <summary>
        /// Đảm bảo camera đã kết nối. Gọi trong _camOpLock.
        /// </summary>
        private bool EnsureCameraReady()
        {
            if (camera.IsConnected) return true;

            string device = string.IsNullOrWhiteSpace(SelectedCameraDevice) ? "default" : SelectedCameraDevice;
            if (!camera.Connect(device)) return false;

            _triggerModeOn = null;
            ApplyCameraSettings();
            return true;
        }

        /// <summary>
        /// Chỉ đổi TriggerMode khi thật sự khác trạng thái hiện tại
        /// (tránh dừng/chạy lại grab ở mỗi lần trigger). Gọi trong _camOpLock.
        /// </summary>
        private void EnsureTriggerMode(bool useTrigger)
        {
            if (_triggerModeOn == useTrigger) return;

            // Không ném lỗi: nếu camera đã ở đúng chế độ thì vẫn chạy bình thường,
            // chỉ báo lên trạng thái để dễ theo dõi.
            if (!camera.ConfigureTrigger(useTrigger, isSoftwareTrigger: true))
                StatusMessage = $"Cảnh báo TriggerMode: {camera.LastError}";

            _triggerModeOn = useTrigger;
        }

        /// <summary>
        /// Chuẩn bị camera ở chế độ Software Trigger rồi bắn 1 xung.
        /// </summary>
        private void RunSoftwareTrigger()
        {
            lock (_camOpLock)
            {
                if (!EnsureCameraReady())
                    throw new InvalidOperationException("Không kết nối được Camera.");

                // Camera này chạy ổn ở TriggerMode = Off (stream liên tục).
                // "Trigger" ở đây = đánh dấu frame kế tiếp là ảnh cần xử lý.
                EnsureTriggerMode(false);
                camera.StartGrabbing();   // không làm gì nếu đang grab

                _isTriggerPending = true;
            }
        }

        // ======================================================================================

        public bool first = true;
        private void OnCameraImageGrabbed(object? sender, HObject rawImage)
        {
            if (rawImage == null || !rawImage.IsInitialized()) return;

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                if (_isTriggerPending)
                {
                    _isTriggerPending = false; // Reset cờ ngay
                    _shotQr = _pendingQr;      // gắn mã QR với ảnh này
                    _shotIndex = _pendingIndex;

                    // 1. Cập nhật UI và gán CurrentImage trên UI Thread
                    dispatcher.Invoke(() =>
                    {
                        CurrentImage?.Dispose();
                        CurrentImage = new HImage(rawImage);

                        // Báo cho View render vào hWindowControl
                        if (first) {
                            SaveProcessedImages(CurrentImage, null, "origin");
                            first = false;
                        }

                    });

                    // 2. Chạy Pipeline duy nhất 1 lần ở Background Thread để tránh đơ UI
                    Task.Run(() =>
                    {
                        try { RunPipeline(); }
                        finally { _shotTcs?.TrySetResult(true); }   // báo đã xử lý xong lần này
                    });
                }
                else if (IsCameraStreaming)
                {
                    dispatcher.Invoke(() =>
                    {
                        LiveImage?.Dispose();
                        LiveImage = new HImage(rawImage);

                        ImageUpdated?.Invoke(LiveImage, true);
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi xử lý ảnh camera: {ex.Message}";
            }
            finally
            {
                rawImage?.Dispose();
            }
        }

        // ============================================================================
        //  MÁY QUÉT -> TRIGGER CAMERA : LUỒNG XỬ LÝ KÝ TỰ KẾT THÚC (ENTER / \r\n)
        // ============================================================================
        // Máy quét gửi:   <mã><\r\n>      (Enter = "đã quét xong")
        //
        // Có 2 đường đưa tín hiệu vào chương trình, cả hai cùng kết thúc ở HandleScannedCode():
        //
        //  (A) Máy quét ở chế độ "USB Virtual COM":
        //        COM  -> OnComDataReceived() -> gom buffer -> cắt tại ReceiveEndChar
        //             -> HandleScannedCode(mã)
        //      * ReceiveEndChar phải KHỚP với máy quét ("\r\n"). Đặt "\r" mà máy gửi "\r\n"
        //        thì "\n" thừa sẽ tạo thêm 1 gói rỗng => trigger 2 lần.
        //
        //  (B) Máy quét ở chế độ "bàn phím" (HID), mã đi vào ứng dụng khác đang focus:
        //        RawKeyboardScanner (nghe song song, không chặn phím) thấy chuỗi gõ nhanh + Enter
        //        -> HandleScannedCode(mã)         (xem chú thích trong RawKeyboardScanner.cs)
        //
        // Sau HandleScannedCode(): mã vào hàng đợi _scanChannel -> ScanWorkerAsync xử lý TUẦN TỰ:
        //   nhận biết pallet -> đánh số hàng -> trigger camera -> chờ pipeline xong -> lần quét kế tiếp.
        //
        // QUY TẮC TRIGGER: chỉ trigger khi lần quét CÓ MÃ đi kèm (<mã><\r\n>).
        //   Enter đứng một mình (không có mã) -> KHÔNG trigger, bị bỏ qua ở HandleScannedCode().
        // ============================================================================

        private string _pendingQr = string.Empty;   // mã QR của lần trigger đang chờ ảnh
        private string _shotQr = string.Empty;      // mã QR gắn với ảnh đang xử lý
        private int _pendingIndex = 0;              // thứ tự hàng trong kiện (cùng mã quét liên tiếp)
        private int _shotIndex = 0;
        private string _lastScanCode = string.Empty;

        // Hàng đợi các lần quét: không bỏ sót khi quét liên tiếp, xử lý tuần tự từng ảnh
        private readonly Channel<string> _scanChannel = Channel.CreateUnbounded<string>();
        private TaskCompletionSource<bool>? _shotTcs;

        private string GetActualEndChar() => ReceiveEndChar switch
        {
            "\\r" => "\r",
            "\\n" => "\n",
            "\\r\\n" => "\r\n",
            _ => ReceiveEndChar   // tự nhập, ví dụ "END"
        };

        // Nhận dữ liệu COM (thread của SerialPort): gom vào buffer, tách gói theo ký tự kết thúc
        private void OnComDataReceived(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var messages = new List<string>();

            lock (_comBufferLock)
            {
                _comReceiveBuffer += data;

                if (UseReceiveEndChar)
                {
                    string end = GetActualEndChar();
                    if (string.IsNullOrEmpty(end)) return;

                    int index;
                    while ((index = _comReceiveBuffer.IndexOf(end, StringComparison.Ordinal)) >= 0)
                    {
                        messages.Add(_comReceiveBuffer.Substring(0, index));
                        _comReceiveBuffer = _comReceiveBuffer.Substring(index + end.Length);
                    }
                }
                else
                {
                    string clean = _comReceiveBuffer.Replace("\r", "").Replace("\n", "").Replace("\0", "").Trim();
                    _comReceiveBuffer = string.Empty;
                    if (clean.Length > 0) messages.Add(clean);
                }
            }

            // Mỗi phần tử trong 'messages' là 1 gói đã được cắt tại ký tự kết thúc (\r\n).
            //  - "ABC123" : mã + Enter            -> hợp lệ, trigger.
            //  - ""       : chỉ có Enter, không mã -> BỎ QUA (HandleScannedCode sẽ không trigger).
            foreach (var m in messages)
                HandleScannedCode(m.Trim());
        }

        /// <summary>
        /// Xử lý một mã quét hoàn chỉnh (từ cổng COM hoặc từ máy quét bàn phím USB).
        /// </summary>
        public void HandleScannedCode(string code)
        {
            // Chỉ trigger khi có mã đi kèm. Chỉ có Enter (mã rỗng / toàn khoảng trắng) -> bỏ qua hoàn toàn:
            // không ghi log, không đổi LastQrCode, không đưa vào hàng đợi, không chụp ảnh.
            if (string.IsNullOrWhiteSpace(code)) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastQrCode = code;
                string line = $"[{DateTime.Now:HH:mm:ss}] {code}\n";
                ReceivedComData += line;
                ReceivedComDataAll += line;
            });

            // Chế độ lọc: chỉ trigger khi mã trùng ComTriggerSignal.
            // (Mặc định TriggerOnAnyScan = true: mọi lần quét CÓ MÃ đều trigger.)
            if (!TriggerOnAnyScan &&
                !string.Equals(code, ComTriggerSignal?.Trim(), StringComparison.Ordinal))
                return;

            // Đưa vào hàng đợi, worker sẽ xử lý lần lượt
            _scanChannel.Writer.TryWrite(code);
        }

        private async Task ScanWorkerAsync()
        {
            await foreach (string code in _scanChannel.Reader.ReadAllAsync())
            {
                try
                {
                    // Cùng mã quét liên tiếp = các hàng khác nhau trong cùng 1 pallet.
                    // Mã khác mã trước = sang pallet mới: chốt kết quả pallet cũ.
                    bool samePallet = string.Equals(code, _lastScanCode, StringComparison.Ordinal);
                    if (!samePallet || !_palletActive)
                    {
                        FinalizePallet();
                        BeginPallet(code);
                    }

                    _pendingIndex = samePallet ? _pendingIndex + 1 : 1;
                    _lastScanCode = code;
                    _pendingQr = code;

                    _shotTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    await Task.Run(RunSoftwareTrigger);

                    // Chờ xử lý xong ảnh này (tối đa 10 giây) rồi mới sang lần quét kế tiếp
                    var finished = await Task.WhenAny(_shotTcs.Task, Task.Delay(10000));
                    if (finished != _shotTcs.Task)
                    {
                        _isTriggerPending = false;
                        StatusMessage = "Quá thời gian chờ ảnh từ camera, bỏ qua lần quét này.";
                    }
                }
                catch (Exception ex)
                {
                    _isTriggerPending = false;
                    StatusMessage = $"Lỗi Trigger: {ex.Message}";
                    if (_comService != null && IsComConnected)
                        _comService.Send(ComResponseError);
                }
            }
        }

        // ================= TỔNG HỢP KẾT QUẢ THEO PALLET =================

        private readonly object _palletLock = new object();

        private bool _palletActive = false;   // true = đang có 1 pallet mở

        private void BeginPallet(string code)
        {
            lock (_palletLock)
            {
                _palletActive = true;
                CurrentPalletCode = code;
                PalletRowCount = 0;
                PalletFoamCount = 0;
                PalletWoodCount = 0;
                PalletErrorCount = 0;
                PalletMaterial = string.Empty;
            }
        }

        /// <summary>Ghi nhận kết quả 1 hàng (chỉ khi ảnh đến từ lần quét máy quét).</summary>
        private void RecordRowResult(string material)
        {
            lock (_palletLock)
            {
                PalletRowCount++;
                if (material == "XỐP") PalletFoamCount++;
                else if (material == "GỖ") PalletWoodCount++;
                else PalletErrorCount++;

                PalletMaterial = ComputePalletMaterial();
            }
        }

        private string ComputePalletMaterial()
        {
            int foam = PalletFoamCount, wood = PalletWoodCount;
            if (foam == 0 && wood == 0) return "KHÔNG XÁC ĐỊNH";

            return PalletRule switch
            {
                "AnyFoam" => foam > 0 ? "XỐP" : "GỖ",
                "Majority" => foam > wood ? "XỐP" : (wood > foam ? "GỖ" : "LẪN"),
                _ => wood > 0 ? "GỖ" : "XỐP"      // AnyWood (mặc định)
            };
        }

        /// <summary>Chốt kết quả pallet hiện tại và ghi 1 dòng vào pallets.csv.</summary>
        private void FinalizePallet()
        {
            string code, result;
            int rows, foam, wood, err;

            lock (_palletLock)
            {
                if (PalletRowCount == 0) return;

                code = CurrentPalletCode;
                rows = PalletRowCount; foam = PalletFoamCount;
                wood = PalletWoodCount; err = PalletErrorCount;
                result = ComputePalletMaterial();
                PalletMaterial = result;
            }

            StatusMessage = $"Pallet {code}: {result} ({rows} hàng: {foam} xốp, {wood} gỗ, {err} lỗi)";

            try
            {
                if (!Directory.Exists(SaveFolderPath)) Directory.CreateDirectory(SaveFolderPath);

                string path = Path.Combine(SaveFolderPath, "pallets.csv");
                bool isNew = !File.Exists(path);
                string safeCode = "\"" + code.Replace("\"", "\"\"") + "\"";

                using var w = new StreamWriter(path, true, new UTF8Encoding(true));
                if (isNew) w.WriteLine("Time,PalletCode,Rows,Foam,Wood,Error,Rule,Result");
                w.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{safeCode},{rows},{foam},{wood},{err},{PalletRule},{result}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi ghi pallets.csv: {ex.Message}";
            }
        }

        /// <summary>Kết thúc pallet hiện tại (dùng cho pallet cuối, hoặc khi pallet sau trùng mã).</summary>
        [RelayCommand]
        public void FinishPallet()
        {
            FinalizePallet();
            _palletActive = false;          // lần quét kế tiếp sẽ bắt đầu pallet mới
            _lastScanCode = string.Empty;
            _pendingIndex = 0;
        }

        private static string SanitizeFileName(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');
            return text.Length > 40 ? text.Substring(0, 40) : text;
        }

        [RelayCommand]
        public void ScanGigECameras()
        {
            CameraList.Clear();
            var devices = Camera.ScanCameras();

            foreach (var dev in devices)
            {
                CameraList.Add(dev);
            }

            if (CameraList.Count > 0)
            {
                SelectedCameraDevice = CameraList.First();
                StatusMessage = $"Tìm thấy {CameraList.Count} Camera GigE.";
            }
            else
            {
                StatusMessage = "Không tìm thấy Camera GigE nào trong mạng!";
            }
        }

        [RelayCommand]
        public async Task StartCamera()
        {
            try
            {
                StatusMessage = "Đang kết nối Camera...";
                await Task.Run(() =>
                {
                    lock (_camOpLock)
                    {
                        if (!EnsureCameraReady())
                            throw new InvalidOperationException("Không kết nối được Camera.");

                        // Chạy liên tục (TriggerMode Off) để nhận Live Stream
                        EnsureTriggerMode(false);
                        camera.StartGrabbing();
                    }
                });

                IsCameraStreaming = true;
                StatusMessage = "Đã bật Live Stream Camera.";
            }
            catch (Exception ex)
            {
                IsCameraStreaming = false;
                StatusMessage = $"Lỗi bật Camera: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task Trigger()
        {
            try
            {
                StatusMessage = "Đang kích hoạt Trigger...";

                // Trigger bằng tay / Auto Timer: không gắn với lần quét nào
                _pendingQr = string.Empty;
                _pendingIndex = 0;

                await Task.Run(RunSoftwareTrigger);

                StatusMessage = "Đã gửi tín hiệu Trigger thành công.";
            }
            catch (Exception ex)
            {
                _isTriggerPending = false;
                StatusMessage = $"Lỗi Trigger: {ex.Message}";
            }
        }

        // Dùng cho nút debug trên UI nếu cần xem thông số camera
        [RelayCommand]
        public void ShowCameraDebug()
        {
            camera.DebugGamma();
        }

        [RelayCommand]
        public void ShowTriggerDebug()
        {
            camera.DebugTrigger();
        }

        [RelayCommand]
        public void ShowCameraParameters()
        {
            camera.PrintSupportedParameters();
        }

        #region ================= COM PORT COMMANDS & LOGIC =================

        [RelayCommand]
        private void RefreshComPorts()
        {
            ComPortList.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                ComPortList.Add(port);
            }

            if (ComPortList.Count > 0 && !ComPortList.Contains(SelectedComPort))
            {
                SelectedComPort = ComPortList.First();
            }
        }

        [RelayCommand]
        public void ConnectCom()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedComPort))
                {
                    StatusMessage = "Vui lòng chọn hoặc nhập tên cổng COM!";
                    return;
                }

                _comService.Connect(SelectedComPort, SelectedBaudRate);

                IsComConnected = true;
                StatusMessage = $"Đã kết nối {SelectedComPort} ({SelectedBaudRate} bps)";
            }
            catch (Exception ex)
            {
                IsComConnected = false;
                StatusMessage = $"Lỗi kết nối COM: {ex.Message}";
            }
        }

        [RelayCommand]
        public void DisconnectCom()
        {
            try
            {
                _comService.Disconnect();
                IsComConnected = false;
                StatusMessage = "Đã ngắt kết nối COM.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi ngắt kết nối COM: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SendComData()
        {
            if (!IsComConnected)
            {
                StatusMessage = "Chưa kết nối Cổng COM!";
                return;
            }

            if (string.IsNullOrEmpty(TextToSendCom)) return;

            try
            {
                _comService.Send(TextToSendCom);
                StatusMessage = $"Đã gửi COM: {TextToSendCom}";
                TextToSendCom = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi gửi dữ liệu COM: {ex.Message}";
            }
        }

        #endregion

        #region ================= IP CAMERA COMMANDS & LOGIC =================

        [RelayCommand]
        public async Task StopCamera()
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (_camOpLock)
                    {
                        camera.Disconnect();
                        _triggerModeOn = null;
                    }
                });
                IsCameraStreaming = false;
                StatusMessage = "Đã tắt Camera.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi tắt Camera: {ex.Message}";
            }
        }

        #endregion

        #region ================= EXISTING COMMANDS =================

        private void ExecuteOpenConnection()
        {
            // Điều hướng hoặc mở View kết nối ở đây
        }

        [RelayCommand]
        private void OpenImage()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                CurrentImage?.Dispose();
                CurrentImage = new HImage(dialog.FileName);
                ImagePath = dialog.FileName;
                StatusMessage = "Image loaded";
            }
            catch (Exception ex) { StatusMessage = $"Load image failed: {ex.Message}"; }
        }

        [RelayCommand]
        private void RunPipeline()
        {
            HImage? croppedRoi = null;

            try
            {
                if (CurrentImage == null || CurrentRegion == null)
                {
                    StatusMessage = "Please load image and set region first";
                    return;
                }

                // 1. Áp dụng ROI vào ảnh (vẫn giữ nguyên kích thước ảnh gốc nhưng chỉ xử lý phần ROI)
                HImage imageReduced = CurrentImage.ReduceDomain(CurrentRegion.Region);

                // 2. Cắt ảnh thật sự ra thành 1 khung hình nhỏ bọc vừa ROI để lưu (CropDomain)
                croppedRoi = imageReduced.CropDomain();

                TextureTool textureTool = new TextureTool();

                // 3. Chạy thuật toán
                TextureResult result = textureTool.Run2(
                    imageReduced,
                    ThreshGrayMean, UseGrayMean, OperatorGrayMean,
                    ThreshGrayDev, UseGrayDev, OperatorGrayDev,
                    ThreshSatMean, UseSatMean, OperatorSatMean,
                    ThreshSatDev, UseSatDev, OperatorSatDev,
                    ThreshTextureMean, UseTextureMean, OperatorTextureMean);

                TextureResult = result;

                TextureMean = result.TextureMean;
                TextureDeviation = result.TextureDeviation;
                SaturationMean = result.SaturationMean;
                SaturationDeviation = result.SaturationDeviation;
                GrayMean = result.GrayMean;
                GrayDeviation = result.GrayDeviation;
                Merterial = result.Merterial;

                // Ảnh đến từ lần quét máy quét -> tính vào pallet hiện tại
                if (_shotTcs != null && !_shotTcs.Task.IsCompleted)
                    RecordRowResult(result.Merterial);

                // 4. Gửi tín hiệu COM
                if (_comService != null && IsComConnected)
                {
                    switch (result.Merterial)
                    {
                        case "XỐP":
                        case "1":
                            _comService.Send(ComResponseXop);
                            break;
                        case "GỖ":
                        case "2":
                            _comService.Send(ComResponseGo);
                            break;
                        default:
                            _comService.Send(ComResponseError);
                            break;
                    }
                }

                // 5. GỌI HÀM LƯU TẠI VM (Lưu cả ảnh gốc và ảnh ROI)
                SaveProcessedImages(CurrentImage, croppedRoi, result.Merterial);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Pipeline error: {ex.Message}";
                if (_shotTcs != null && !_shotTcs.Task.IsCompleted)
                    RecordRowResult("ERROR");
                if (_comService != null && IsComConnected)
                {
                    _comService.Send(ComResponseError);
                }
            }
            finally
            {
                // Giải phóng ảnh cắt sau khi dùng xong
                croppedRoi?.Dispose();
            }
        }

        [RelayCommand]
        private void Clear()
        {
            CurrentResult = null;
            StatusMessage = "Ready";
        }

        [RelayCommand]
        public async Task ConnectCamera()
        {
            await StartCamera();
        }

        [RelayCommand]
        public void Start(string portName)
        {
            SelectedComPort = portName;
            ConnectCom();
        }

        // ================= CHỌN THƯ MỤC VÀ LƯU ẢNH =================

        [RelayCommand]
        private void SelectFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục để lưu ảnh đã xử lý",
                InitialDirectory = string.IsNullOrEmpty(SaveFolderPath) ? "C:\\" : SaveFolderPath
            };

            if (dialog.ShowDialog() == true)
            {
                SaveFolderPath = dialog.FolderName;
                StatusMessage = $"Đã cập nhật thư mục lưu ảnh: {SaveFolderPath}";
            }
        }

        private void SaveProcessedImages(HImage originalImg, HImage roiImg, string materialResult)
        {
            if (originalImg == null || !originalImg.IsInitialized()) return;

            try
            {
                // 1. Kiểm tra và tạo thư mục nếu chưa tồn tại
                if (!Directory.Exists(SaveFolderPath))
                {
                    Directory.CreateDirectory(SaveFolderPath);
                }

                string safeMaterialName = string.IsNullOrEmpty(materialResult) ? "UNKNOWN" : materialResult;
                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                // Ảnh từ lần quét: <mã>_<số thứ tự hàng>_ ; trigger tay / Auto Timer: không có phần này
                string qrPart = string.IsNullOrEmpty(_shotQr) ? string.Empty : $"{SanitizeFileName(_shotQr)}_{_shotIndex:D3}_";

                // 2. Tên file cho Ảnh Gốc
                string originalFileName = $"{timeStamp}_{qrPart}{safeMaterialName}_Original.png";
                string originalPath = Path.Combine(SaveFolderPath, originalFileName);

                // Lưu ảnh gốc
                originalImg.WriteImage("png", 0, originalPath);

                // 3. Tên file cho Ảnh Cắt ROI
                if (roiImg != null && roiImg.IsInitialized())
                {
                    string roiFileName = $"{timeStamp}_{qrPart}{safeMaterialName}_ROI.png";
                    string roiPath = Path.Combine(SaveFolderPath, roiFileName);

                    // Lưu ảnh ROI
                    roiImg.WriteImage("png", 0, roiPath);
                }

                StatusMessage = $"Đã lưu ảnh phân tích: {safeMaterialName} vào {SaveFolderPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi lưu ảnh: {ex.Message}";
            }
        }

        // ================= LƯU / TẢI VÙNG CHỌN (ROI) =================

        private string RegionFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appconfig_roi.hobj");

        private void SaveRegion()
        {
            try
            {
                var region = CurrentRegion?.Region;
                if (region != null && region.IsInitialized())
                {
                    region.WriteRegion(RegionFilePath);     // định dạng .hobj của HALCON
                }
                else if (File.Exists(RegionFilePath))
                {
                    File.Delete(RegionFilePath);            // không còn vùng chọn -> xóa file cũ
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi lưu vùng chọn: {ex.Message}";
            }
        }

        private void LoadRegion()
        {
            try
            {
                if (!File.Exists(RegionFilePath)) return;

                var region = new HRegion();
                region.ReadRegion(RegionFilePath);

                // Lấy lại tọa độ hình chữ nhật bao quanh vùng để điền Row1/Column1/Row2/Column2
                HOperatorSet.SmallestRectangle1(region, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);

                CurrentRegion?.Region?.Dispose();
                CurrentRegion = new RegionResult
                {
                    Row1 = r1.D,
                    Column1 = c1.D,
                    Row2 = r2.D,
                    Column2 = c2.D,
                    Region = region
                };
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi tải vùng chọn: {ex.Message}";
            }
        }

        /// <summary>
        /// Gọi 1 lần khi khởi động: có file cấu hình thì nạp; chưa có thì tự tạo file
        /// từ các giá trị mặc định đang khai báo trong code.
        /// </summary>
        private void InitConfig()
        {
            if (File.Exists(_configFilePath))
            {
                LoadConfig();
            }
            else
            {
                SaveConfig();   // ghi các giá trị mặc định hiện tại ra appconfig.json
                StatusMessage = $"Chưa có cấu hình, đã tạo file mặc định: {_configFilePath}";
            }
        }

        [RelayCommand]
        public void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    SelectedComPort = this.SelectedComPort,
                    SelectedBaudRate = this.SelectedBaudRate,
                    ComTriggerSignal = this.ComTriggerSignal,
                    ComResponseXop = this.ComResponseXop,
                    ComResponseGo = this.ComResponseGo,
                    ComResponseError = this.ComResponseError,

                    // Trước đây thiếu 2 dòng này nên LoadConfig luôn tắt UseReceiveEndChar
                    UseReceiveEndChar = this.UseReceiveEndChar,
                    ReceiveEndChar = this.ReceiveEndChar,

                    SelectedCameraDevice = this.SelectedCameraDevice,
                    CamExposure = this.CamExposure,
                    CamGain = this.CamGain,
                    CamFrameRate = this.CamFrameRate,
                    CamGamma = this.CamGamma,
                    CamContrast = this.CamContrast,

                    ThreshGrayMean = this.ThreshGrayMean,
                    ThreshGrayDev = this.ThreshGrayDev,
                    ThreshSatMean = this.ThreshSatMean,
                    ThreshSatDev = this.ThreshSatDev,
                    ThreshTextureMean = this.ThreshTextureMean,

                    UseGrayMean = this.UseGrayMean,
                    UseGrayDev = this.UseGrayDev,
                    UseSatMean = this.UseSatMean,
                    UseSatDev = this.UseSatDev,
                    UseTextureMean = this.UseTextureMean,

                    OperatorGrayMean = this.OperatorGrayMean,
                    OperatorGrayDev = this.OperatorGrayDev,
                    OperatorSatMean = this.OperatorSatMean,
                    OperatorSatDev = this.OperatorSatDev,
                    OperatorTextureMean = this.OperatorTextureMean,

                    AutoTimerIntervalSeconds = this.AutoTimerIntervalSeconds,
                    TriggerOnAnyScan = this.TriggerOnAnyScan,
                    SaveFolderPath = this.SaveFolderPath
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
                SaveRegion();

                StatusMessage = "Đã lưu cấu hình thành công.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi lưu cấu hình: {ex.Message}";
            }
        }

        [RelayCommand]
        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configFilePath)) return;

                string json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config == null) return;

                this.SelectedComPort = config.SelectedComPort;
                this.SelectedBaudRate = config.SelectedBaudRate;
                this.ComTriggerSignal = config.ComTriggerSignal;
                this.ComResponseXop = config.ComResponseXop;
                this.ComResponseGo = config.ComResponseGo;
                this.ComResponseError = config.ComResponseError;

                this.SelectedCameraDevice = config.SelectedCameraDevice;
                // Kẹp theo dải thật của camera: file cũ thiếu trường (giá trị 0) hoặc sửa tay sai
                // sẽ không đẩy giá trị ngoài dải xuống camera.
                this.CamExposure = Math.Clamp(config.CamExposure, 0, 300000);
                this.CamGain = Math.Clamp(config.CamGain, 1.0, 8.0);
                this.CamFrameRate = config.CamFrameRate > 0 ? config.CamFrameRate : 30.0;
                this.CamGamma = Math.Clamp(config.CamGamma, 0, 255);
                this.CamContrast = Math.Clamp(config.CamContrast, 0, 200);

                this.ThreshGrayMean = config.ThreshGrayMean;
                this.ThreshGrayDev = config.ThreshGrayDev;
                this.ThreshSatMean = config.ThreshSatMean;
                this.ThreshSatDev = config.ThreshSatDev;
                this.ThreshTextureMean = config.ThreshTextureMean;

                this.UseGrayMean = config.UseGrayMean;
                this.UseGrayDev = config.UseGrayDev;
                this.UseSatMean = config.UseSatMean;
                this.UseSatDev = config.UseSatDev;
                this.UseTextureMean = config.UseTextureMean;

                this.OperatorGrayMean = config.OperatorGrayMean;
                this.OperatorGrayDev = config.OperatorGrayDev;
                this.OperatorSatMean = config.OperatorSatMean;
                this.OperatorSatDev = config.OperatorSatDev;
                this.OperatorTextureMean = config.OperatorTextureMean;

                this.SaveFolderPath = config.SaveFolderPath;
                this.UseReceiveEndChar = config.UseReceiveEndChar;
                this.ReceiveEndChar = string.IsNullOrEmpty(config.ReceiveEndChar) ? "\\r\\n" : config.ReceiveEndChar;
                this.AutoTimerIntervalSeconds = config.AutoTimerIntervalSeconds;

                this.TriggerOnAnyScan= config.TriggerOnAnyScan;

                LoadRegion();

                StatusMessage = "Đã tải cấu hình.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi tải cấu hình: {ex.Message}";
            }
        }
        #endregion
    }
}
