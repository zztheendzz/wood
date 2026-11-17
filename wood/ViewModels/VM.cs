using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Nodify;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

        [ObservableProperty] private bool useLineEnding = true;
        [ObservableProperty] private string comLineEnding = "\\n";

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

        // ================= LOG COM (TX / RX / ERR) =================
        // Mỗi dòng log:
        //   [HH:mm:ss.fff] TX   "6"       | kết thúc: <LF> (0A)
        //   [HH:mm:ss.fff] RX   "ABC123"  | kết thúc: <CR><LF> (0D 0A)
        //   [HH:mm:ss.fff] RX   ""        | kết thúc: <CR><LF> (0D 0A) -> chỉ có Enter, bỏ qua
        //   [HH:mm:ss.fff] ERR  Lỗi Trigger: ...
        // Ký tự điều khiển nằm TRONG dữ liệu cũng được hiện ra (vd "<LF>ABC") để thấy ngay
        // khi ký tự kết thúc cấu hình không khớp với thiết bị.
        // Ghi lên UI (ReceivedComDataAll) và ra file <SaveFolderPath>\yyyy-MM-dd\com_yyyyMMdd.log

        private const int MaxUiLogLength = 30000;          // giữ ~30k ký tự cuối trên UI
        private readonly object _logFileLock = new object();

        /// <summary>Đổi ký tự điều khiển thành dạng nhìn thấy được: \r -> &lt;CR&gt;, \n -> &lt;LF&gt;...</summary>
        private static string ToVisible(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                sb.Append(c switch
                {
                    '\r' => "<CR>",
                    '\n' => "<LF>",
                    '\t' => "<TAB>",
                    '\0' => "<NUL>",
                    _ when c < 0x20 || c == 0x7F => $"<0x{(int)c:X2}>",
                    _ => c.ToString()
                });
            }
            return sb.ToString();
        }

        /// <summary>Mô tả ký tự kết thúc: "&lt;CR&gt;&lt;LF&gt; (0D 0A)"; rỗng -> "(không có)".</summary>
        private static string DescribeEndChar(string? end)
        {
            if (string.IsNullOrEmpty(end)) return "(không có)";
            string hex = string.Join(" ", Encoding.UTF8.GetBytes(end).Select(b => b.ToString("X2")));
            return $"{ToVisible(end)} ({hex})";
        }

        private void ComLog(string kind, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {kind,-4} {message}";

            // 1. UI (luôn cập nhật trên UI thread, không chặn thread COM)
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                string all = ReceivedComDataAll + line + "\n";
                if (all.Length > MaxUiLogLength)
                    all = all.Substring(all.Length - MaxUiLogLength);
                ReceivedComDataAll = all;
            });

            // 2. File
            try
            {
                // Log nằm chung thư mục ngày với ảnh: <SaveFolderPath>\yyyy-MM-dd\com_yyyyMMdd.log
                string folder = GetTodayFolder();
                string path = Path.Combine(folder, $"com_{DateTime.Now:yyyyMMdd}.log");

                lock (_logFileLock)
                {
                    File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd} {line}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Không để lỗi ghi log làm hỏng luồng chính, nhưng phải báo ra (tối đa 1 lần / 10 giây)
                if ((DateTime.Now - _lastLogFileError).TotalSeconds > 10)
                {
                    _lastLogFileError = DateTime.Now;
                    StatusMessage = $"Lỗi ghi file log ({GetBaseFolder()}): {ex.Message}";
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                        ReceivedComDataAll += $"[{DateTime.Now:HH:mm:ss.fff}] ERR  Không ghi được file log: {ex.Message}\n");
                }
            }
        }

        private DateTime _lastLogFileError = DateTime.MinValue;

        private void LogTx(string payload, string endChar) =>
            ComLog("TX", $"\"{ToVisible(payload)}\" | kết thúc: {DescribeEndChar(endChar)}");

        private void LogRx(string payload, string endChar, string note = "") =>
            ComLog("RX", $"\"{ToVisible(payload)}\" | kết thúc: {DescribeEndChar(endChar)}{note}");

        private void LogInfo(string message) => ComLog("INFO", message);

        /// <summary>Ghi lỗi vào log và đồng thời hiện lên StatusMessage.</summary>
        private void LogError(string message)
        {
            StatusMessage = message;
            ComLog("ERR", message);
        }

        [RelayCommand]
        private void ClearComLog()
        {
            ReceivedComDataAll = string.Empty;
            ReceivedComData = string.Empty;
        }

        // --- CẤU HÌNH CHẾ ĐỘ AUTO TIMER ---
        [ObservableProperty]
        private bool isAutoTimerRunning = false;

        [ObservableProperty]
        private double autoTimerIntervalSeconds = 2.0; // Mặc định 2 giây chụp 1 lần

        private System.Threading.Timer? _autoTimer;

        // --- Cờ cho Barcode Mode ---
        [ObservableProperty]
        private bool isBarcodeModeActive = false;

        [ObservableProperty]
        private HImage? croppedImage;

        #endregion

        public VM()
        {
            _comService = new Com();
            _pipeline = new VisionPipeline();
            _pipeline.Add(new TextureTool());

            // Đăng ký sự kiện nhận dữ liệu từ COMe
            _comService.DataReceived += OnComDataReceived;

            camera.OnImageGrabbed += OnCameraImageGrabbed;
            camera.OnError += (s, err) => LogError($"Camera: {err}");

            RefreshComPorts();

            // Tự nạp cấu hình khi chạy chương trình; chưa có file thì tự tạo từ giá trị mặc định trong code
            InitConfig();

            // Dọn thư mục ngày cũ ngay khi mở chương trình
            _ = Task.Run(() => { try { GetTodayFolder(); } catch { } });

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
                        if (first)
                        {
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

        private static string ParseEndChar(string? s) => s switch
        {
            "\\r" => "\r",
            "\\n" => "\n",
            "\\r\\n" => "\r\n",
            _ => s ?? string.Empty   // tự nhập, ví dụ "END"
        };

        private string GetActualEndChar() => ParseEndChar(ReceiveEndChar);

        private string GetActualSendEndChar() => UseLineEnding ? ParseEndChar(ComLineEnding) : string.Empty;

        /// <summary>
        /// Mọi lệnh gửi COM đều đi qua đây: luôn kèm ký tự kết thúc và luôn được ghi log.
        /// Trả về true nếu gửi thành công.
        /// </summary>
        private bool SendCom(string data)
        {
            if (string.IsNullOrEmpty(data)) return false;

            string end = GetActualSendEndChar();

            if (_comService == null || !IsComConnected)
            {
                ComLog("ERR", $"TX bỏ qua (COM chưa kết nối): \"{ToVisible(data)}\" | kết thúc: {DescribeEndChar(end)}");
                return false;
            }

            try
            {
                _comService.Send(data + end);
                LogTx(data, end);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Lỗi gửi COM \"{ToVisible(data)}\" | kết thúc: {DescribeEndChar(end)}: {ex.Message}");
                return false;
            }
        }

        // Nhận dữ liệu COM (thread của SerialPort): gom vào buffer, tách gói theo ký tự kết thúc
        private void OnComDataReceived(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var messages = new List<string>();
            string end = string.Empty;
            string rawWhenNoEndChar = string.Empty;

            lock (_comBufferLock)
            {
                _comReceiveBuffer += data;

                if (UseReceiveEndChar)
                {
                    end = GetActualEndChar();
                    if (string.IsNullOrEmpty(end))
                    {
                        ComLog("ERR", $"RX chưa cấu hình ký tự kết thúc, dữ liệu đang chờ: \"{ToVisible(_comReceiveBuffer)}\"");
                        return;
                    }

                    int index;
                    while ((index = _comReceiveBuffer.IndexOf(end, StringComparison.Ordinal)) >= 0)
                    {
                        messages.Add(_comReceiveBuffer.Substring(0, index));
                        _comReceiveBuffer = _comReceiveBuffer.Substring(index + end.Length);
                    }
                }
                else
                {
                    rawWhenNoEndChar = _comReceiveBuffer;
                    string clean = _comReceiveBuffer.Replace("\r", "").Replace("\n", "").Replace("\0", "").Trim();
                    _comReceiveBuffer = string.Empty;
                    if (clean.Length > 0) messages.Add(clean);
                }
            }

            // Tắt cắt gói: log nguyên chuỗi thô để vẫn thấy thiết bị gửi ký tự kết thúc gì
            if (!UseReceiveEndChar)
            {
                ComLog("RX", $"\"{ToVisible(rawWhenNoEndChar)}\" | kết thúc: (tắt cắt gói - đã bỏ <CR>/<LF>/<NUL>)");
                foreach (var m in messages)
                    ProcessScannedCode(m);
                return;
            }

            // Mỗi phần tử trong 'messages' là 1 gói đã được cắt tại ký tự kết thúc.
            //  - "ABC123" : mã + Enter            -> hợp lệ, trigger.
            //  - ""       : chỉ có Enter, không mã -> BỎ QUA (chỉ ghi log).
            // Log nội dung TRƯỚC khi Trim để nhìn thấy ký tự thừa (vd "<LF>ABC" = cấu hình sai end char).
            foreach (var m in messages)
            {
                string code = m.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    LogRx(m, end, " -> chỉ có ký tự kết thúc, không có mã, bỏ qua");
                    continue;
                }

                LogRx(m, end);
                ProcessScannedCode(code);
            }
        }

        /// <summary>
        /// Xử lý một mã quét hoàn chỉnh (từ cổng COM hoặc từ máy quét bàn phím USB).
        /// </summary>
        public void HandleScannedCode(string code)
        {
            // Lối vào cho máy quét chế độ bàn phím (HID, RawKeyboardScanner): mã kết thúc bằng phím Enter.
            if (string.IsNullOrWhiteSpace(code))
            {
                ComLog("RX", "[HID] \"\" | kết thúc: <Enter> -> không có mã, bỏ qua");
                return;
            }

            ComLog("RX", $"[HID] \"{ToVisible(code)}\" | kết thúc: <Enter>");
            ProcessScannedCode(code.Trim());
        }

        /// <summary>Xử lý mã đã hợp lệ (đã log RX ở nơi gọi).</summary>
        private void ProcessScannedCode(string code)
        {
            // Chỉ trigger khi có mã đi kèm.
            if (string.IsNullOrWhiteSpace(code)) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastQrCode = code;
                ReceivedComData += $"[{DateTime.Now:HH:mm:ss}] {code}\n";
            });

            // Chế độ lọc: chỉ trigger khi mã trùng ComTriggerSignal.
            // (Mặc định TriggerOnAnyScan = true: mọi lần quét CÓ MÃ đều trigger.)
            if (!TriggerOnAnyScan &&
                !string.Equals(code, ComTriggerSignal?.Trim(), StringComparison.Ordinal))
            {
                LogInfo($"Mã \"{ToVisible(code)}\" khác tín hiệu trigger \"{ToVisible(ComTriggerSignal)}\" -> không chụp");
                return;
            }

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
                        LogError($"Quá thời gian chờ ảnh từ camera (mã \"{ToVisible(code)}\", hàng {_pendingIndex}), bỏ qua lần quét này.");
                    }
                }
                catch (Exception ex)
                {
                    _isTriggerPending = false;
                    LogError($"Lỗi Trigger (mã \"{ToVisible(code)}\"): {ex.Message}");
                    // Trước đây gửi mã lỗi 2 lần (1 lần không có ký tự kết thúc) -> chỉ gửi 1 lần qua SendCom
                    SendCom(ComResponseError);
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
                LogError($"Lỗi ghi pallets.csv: {ex.Message}");
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
                LogError($"Lỗi bật Camera: {ex.Message}");
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
                LogError($"Lỗi Trigger: {ex.Message}");
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
                LogInfo($"Kết nối {SelectedComPort} ({SelectedBaudRate} bps) | " +
                        $"kết thúc nhận: {(UseReceiveEndChar ? DescribeEndChar(GetActualEndChar()) : "(tắt)")} | " +
                        $"kết thúc gửi: {DescribeEndChar(GetActualSendEndChar())}");
            }
            catch (Exception ex)
            {
                IsComConnected = false;
                LogError($"Lỗi kết nối COM {SelectedComPort}: {ex.Message}");
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
                LogInfo($"Ngắt kết nối {SelectedComPort}");
            }
            catch (Exception ex)
            {
                LogError($"Lỗi ngắt kết nối COM: {ex.Message}");
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

            // SendCom tự bắt lỗi và ghi log
            if (SendCom(TextToSendCom))
            {
                StatusMessage = $"Đã gửi COM: {TextToSendCom} + {DescribeEndChar(GetActualSendEndChar())}";
                TextToSendCom = string.Empty;
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
                    LogError("Pipeline: chưa có ảnh hoặc chưa chọn vùng xử lý (ROI) -> không gửi kết quả");
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
                        case "XỐP": SendCom(ComResponseXop); break;
                        case "GỖ": SendCom(ComResponseGo); break;
                        default: SendCom(ComResponseError); break;
                    }
                }

                // 5. GỌI HÀM LƯU TẠI VM (Lưu cả ảnh gốc và ảnh ROI)
                SaveProcessedImages(CurrentImage, croppedRoi, result.Merterial);

                CroppedImage?.Dispose();
                CroppedImage = new HImage(croppedRoi);
            }
            catch (Exception ex)
            {
                LogError($"Pipeline error: {ex.Message}");
                if (_shotTcs != null && !_shotTcs.Task.IsCompleted)
                    RecordRowResult("ERROR");
                // Trước đây gửi mã lỗi 2 lần (1 lần không có ký tự kết thúc) -> chỉ gửi 1 lần qua SendCom
                SendCom(ComResponseError);
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

        // ================= THƯ MỤC THEO NGÀY + TỰ XOÁ NGÀY CŨ =================
        // Cấu trúc:
        //   <SaveFolderPath>\
        //       pallets.csv
        //       2026-11-16\  (ảnh *_Original.png, *_ROI.png + com_20261116.log)
        //       2026-11-17\  ...
        // Chỉ giữ KeepDays thư mục ngày gần nhất; thừa thì xoá thư mục ngày xa nhất.
        // Chỉ xoá thư mục có tên đúng dạng yyyy-MM-dd, không bao giờ xoá thư mục hôm nay.

        private const int KeepDays = 30;
        private const string DayFolderFormat = "yyyy-MM-dd";

        private readonly object _dayFolderLock = new object();
        private string _lastCleanupKey = string.Empty;   // "<thư mục gốc>|<ngày>" đã dọn

        private string GetBaseFolder() =>
            string.IsNullOrWhiteSpace(SaveFolderPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data")
                : SaveFolderPath;

        /// <summary>Trả về (và tạo nếu chưa có) thư mục của ngày hôm nay. Sang ngày mới sẽ tự dọn ngày cũ.</summary>
        private string GetTodayFolder()
        {
            string baseFolder = GetBaseFolder();
            string today = DateTime.Now.ToString(DayFolderFormat, CultureInfo.InvariantCulture);
            string folder = Path.Combine(baseFolder, today);
            Directory.CreateDirectory(folder);

            bool needCleanup = false;
            lock (_dayFolderLock)
            {
                string key = baseFolder + "|" + today;
                if (_lastCleanupKey != key)
                {
                    _lastCleanupKey = key;
                    needCleanup = true;
                }
            }

            // Dọn ở background, không làm chậm lưu ảnh / ghi log
            if (needCleanup)
                _ = Task.Run(() => CleanupOldDayFolders(baseFolder, today));

            return folder;
        }

        private void CleanupOldDayFolders(string baseFolder, string today)
        {
            try
            {
                if (!Directory.Exists(baseFolder)) return;

                var dayFolders = Directory.GetDirectories(baseFolder)
                    .Select(p => new { Path = p, Name = Path.GetFileName(p) })
                    .Where(x => DateTime.TryParseExact(x.Name, DayFolderFormat, CultureInfo.InvariantCulture,
                                                       DateTimeStyles.None, out _))
                    .OrderByDescending(x => x.Name)   // yyyy-MM-dd sắp theo chữ = sắp theo ngày
                    .ToList();

                // Giữ KeepDays ngày mới nhất, xoá phần còn lại (ngày xa nhất)
                foreach (var old in dayFolders.Skip(KeepDays))
                {
                    if (old.Name == today) continue;   // an toàn: không xoá hôm nay
                    try
                    {
                        Directory.Delete(old.Path, recursive: true);
                        ComLog("INFO", $"Đã xoá thư mục ngày cũ (giữ {KeepDays} ngày): {old.Path}");
                    }
                    catch (Exception ex)
                    {
                        ComLog("ERR", $"Không xoá được thư mục ngày cũ {old.Path}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ComLog("ERR", $"Lỗi dọn thư mục ngày cũ: {ex.Message}");
            }
        }

        private void SaveProcessedImages(HImage originalImg, HImage roiImg, string materialResult)
        {
            if (originalImg == null || !originalImg.IsInitialized()) return;

            try
            {
                // 1. Thư mục của ngày hôm nay (tự tạo, tự dọn ngày cũ)
                string dayFolder = GetTodayFolder();

                string safeMaterialName = string.IsNullOrEmpty(materialResult) ? "UNKNOWN" : materialResult;
                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                // Ảnh từ lần quét: <mã>_<số thứ tự hàng>_ ; trigger tay / Auto Timer: không có phần này
                string qrPart = string.IsNullOrEmpty(_shotQr) ? string.Empty : $"{SanitizeFileName(_shotQr)}_{_shotIndex:D3}_";

                // 2. Tên file cho Ảnh Gốc
                string originalFileName = $"{timeStamp}_{qrPart}{safeMaterialName}_Original.png";
                string originalPath = Path.Combine(dayFolder, originalFileName);

                // Lưu ảnh gốc
                originalImg.WriteImage("png", 0, originalPath);

                // 3. Tên file cho Ảnh Cắt ROI
                if (roiImg != null && roiImg.IsInitialized())
                {
                    string roiFileName = $"{timeStamp}_{qrPart}{safeMaterialName}_ROI.png";
                    string roiPath = Path.Combine(dayFolder, roiFileName);

                    // Lưu ảnh ROI
                    roiImg.WriteImage("png", 0, roiPath);
                }

                StatusMessage = $"Đã lưu ảnh phân tích: {safeMaterialName} vào {dayFolder}";
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
                    SaveFolderPath = this.SaveFolderPath,

                    UseLineEnding = this.UseLineEnding,
                    ComLineEnding = this.ComLineEnding,

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
        /// <summary>
        /// Cắt ảnh dựa trên CurrentImage và CurrentRegion hiện tại để hiển thị lên cửa sổ Crop
        /// </summary>
        public void UpdateCroppedImage()
        {
            if (CurrentImage == null || !CurrentImage.IsInitialized() ||
                CurrentRegion?.Region == null || !CurrentRegion.Region.IsInitialized())
            {
                return;
            }

            try
            {
                // 1. Áp dụng vùng chọn vào ảnh gốc
                using HImage reduced = CurrentImage.ReduceDomain(CurrentRegion.Region);

                // 2. Cắt khung hình bao quanh vùng chọn
                using HImage cropped = reduced.CropDomain();

                // 3. Gán vào thuộc tính để View hiển thị
                CroppedImage?.Dispose();
                CroppedImage = new HImage(cropped);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi cắt ảnh ROI: {ex.Message}";
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

                this.TriggerOnAnyScan = config.TriggerOnAnyScan;

                this.UseLineEnding = config.UseLineEnding;
                this.ComLineEnding = string.IsNullOrEmpty(config.ComLineEnding) ? "\\n" : config.ComLineEnding;

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
