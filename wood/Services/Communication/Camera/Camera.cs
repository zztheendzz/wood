using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace wood.Services.Communication.Camera
{
    public partial class Camera : IDisposable
    {
        private HTuple _hAcqHandle = null;
        private CancellationTokenSource _cts = null;
        private Task _grabTask = null;
        private volatile bool _isGrabbing = false;
        private bool _disposed = false;
        private readonly object _lockObj = new object();

        public string IpAddress { get; set; }
        public int Port { get; set; }

        public event EventHandler<HObject> OnImageGrabbed;
        public event EventHandler<string> OnError;

        public bool IsConnected
        {
            get { lock (_lockObj) { return _hAcqHandle != null; } }
        }

        public bool IsGrabbing => _isGrabbing;

        public Camera() { }

        public Camera(string ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;
        }

        #region Scan / Connect / Disconnect

        /// <summary>
        /// Quét tất cả Camera GigE Vision đang kết nối trong mạng
        /// </summary>
        public static List<string> ScanCameras()
        {
            var cameraList = new List<string>();
            try
            {
                HOperatorSet.InfoFramegrabber("GigEVision2", "device", out HTuple information, out HTuple valueList);

                for (int i = 0; i < valueList.Length; i++)
                {
                    string deviceName = valueList[i].S;
                    if (!string.IsNullOrWhiteSpace(deviceName))
                        cameraList.Add(deviceName);
                }
            }
            catch (HalconException ex)
            {
                MessageBox.Show(ex.ToString());
                System.Diagnostics.Debug.WriteLine($"Lỗi quét Camera: {ex.Message}");
            }

            return cameraList;
        }

        public bool Connect(string cameraName = "default")
        {
            // Ngắt kết nối cũ NGOÀI lock (Disconnect -> StopGrabbing có Wait,
            // nếu giữ lock thì GrabLoop không lấy được lock => treo)
            if (IsConnected) Disconnect();

            lock (_lockObj)
            {
                try
                {
                    string deviceToOpen = string.IsNullOrEmpty(cameraName) ? "default" : cameraName;

                    HOperatorSet.OpenFramegrabber(
                        "GigEVision2",
                        0, 0, 0, 0, 0, 0,
                        "default",
                        -1,
                        "default",
                        -1,
                        "false",
                        "default",
                        deviceToOpen,
                        0,
                        -1,
                        out _hAcqHandle
                    );

                    return true;
                }
                catch (HalconException ex)
                {
                    _hAcqHandle = null;
                    OnError?.Invoke(this, $"Lỗi OpenFramegrabber: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    _hAcqHandle = null;
                    OnError?.Invoke(this, $"Lỗi không xác định khi kết nối Camera: {ex.Message}");
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            StopGrabbing();

            lock (_lockObj)
            {
                if (_hAcqHandle != null)
                {
                    try { HOperatorSet.CloseFramegrabber(_hAcqHandle); }
                    catch (HalconException) { }
                    finally { _hAcqHandle = null; }
                }
            }
        }

        #endregion

        #region Grabbing

        public void StartGrabbing()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null || _isGrabbing) return;

                _cts = new CancellationTokenSource();
                _isGrabbing = true;

                var token = _cts.Token;
                _grabTask = Task.Run(() => GrabLoop(token));
            }
        }

        private void GrabLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HTuple localHandle;
                lock (_lockObj) { localHandle = _hAcqHandle; }
                if (localHandle == null) break;

                HObject image = null;
                try
                {
                    // Timeout hữu hạn để loop luôn thoát được khi Cancel
                    HOperatorSet.GrabImageAsync(out image, localHandle, 500.0);

                    if (image != null && image.IsInitialized())
                    {
                        // Người nhận event chịu trách nhiệm Dispose ảnh này
                        OnImageGrabbed?.Invoke(this, image.Clone());
                    }
                }
                catch (HalconException ex)
                {
                    if (token.IsCancellationRequested) break;

                    // Timeout (chưa có frame/trigger) thì bỏ qua, tiếp tục
                    if (ex.Message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    OnError?.Invoke(this, $"Lỗi trong quá trình Grab: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    // Lỗi từ handler của OnImageGrabbed không được làm chết loop âm thầm
                    OnError?.Invoke(this, $"Lỗi xử lý ảnh: {ex.Message}");
                }
                finally
                {
                    image?.Dispose();
                }
            }

            if (!token.IsCancellationRequested)
                _isGrabbing = false;   // loop tự thoát do lỗi
        }

        public void StopGrabbing()
        {
            CancellationTokenSource cts;
            Task task;
            lock (_lockObj)
            {
                cts = _cts;
                task = _grabTask;
                _cts = null;
                _grabTask = null;
                _isGrabbing = false;
            }

            if (cts == null) return;

            try { cts.Cancel(); } catch { }
            try { task?.Wait(2000); } catch { }
            cts.Dispose();
        }

        public HObject SnapShot()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null) return null;

                try
                {
                    HOperatorSet.GrabImage(out HObject image, _hAcqHandle);
                    return image;
                }
                catch (HalconException ex)
                {
                    OnError?.Invoke(this, $"Lỗi SnapShot: {ex.Message}");
                    return null;
                }
            }
        }

        #endregion

        #region Camera Parameters

        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

        /// <summary>
        /// Dừng grab, set tham số, đọc lại để kiểm tra, rồi chạy lại grab.
        /// </summary>
        // Chỉ cho phép 1 lệnh set tham số chạy tại một thời điểm
        // (tránh nhiều Stop/Start grab chồng chéo khi kéo slider)
        private readonly object _paramGate = new object();

        private bool SetParam(string name, HTuple value, string label)
        {
            lock (_paramGate)
            {
                // 1. Thử set trực tiếp, không dừng grab (không bị khựng live)
                if (TryApply(name, value, label, out string error)) return true;

                // 2. Nếu camera từ chối khi đang acquisition -> dừng grab, set lại, chạy lại
                if (_isGrabbing)
                {
                    StopGrabbing();   // gọi NGOÀI _lockObj
                    try
                    {
                        if (TryApply(name, value, label, out error)) return true;
                    }
                    finally
                    {
                        StartGrabbing();
                    }
                }

                OnError?.Invoke(this, $"Không thể chỉnh {label}: {error}");
                return false;
            }
        }

        private bool TryApply(string name, HTuple value, string label, out string error)
        {
            error = string.Empty;
            lock (_lockObj)
            {
                if (_hAcqHandle == null)
                {
                    error = "Camera chưa kết nối";
                    return false;
                }

                try
                {
                    HOperatorSet.SetFramegrabberParam(_hAcqHandle, name, value);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, name, out HTuple actual);
                    System.Diagnostics.Debug.WriteLine($"{label}: yêu cầu {value}, thực tế {actual}");
                    return true;
                }
                catch (HalconException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        // Double: 0..300000 µs
        public bool SetExposureTime(double us)
            => SetParam("ExposureTime", new HTuple(Clamp(us, 0, 300000)), "Exposure");

        // Double: 1..8
        public bool SetGain(double gain)
            => SetParam("Gain", new HTuple(Clamp(gain, 1.0, 8.0)), "Gain");

        // Int: 0..255
        public bool SetGamma(int raw)
            => SetParam("Gamma", new HTuple((int)Clamp(raw, 0, 255)), "Gamma");

        // Int: 0..200
        public bool SetContrast(int raw)
            => SetParam("Contrast", new HTuple((int)Clamp(raw, 0, 200)), "Contrast");

        public bool SetFrameRate(double fps)
        {
            bool wasGrabbing = _isGrabbing;
            if (wasGrabbing) StopGrabbing();

            try
            {
                lock (_lockObj)
                {
                    if (_hAcqHandle == null) return false;

                    // Bật cho phép chỉnh frame rate (bỏ qua lỗi nếu camera không hỗ trợ)
                    TrySet("AcquisitionFrameRateEnable", "true");

                    HOperatorSet.SetFramegrabberParam(_hAcqHandle, "AcquisitionFrameRate", fps);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "AcquisitionFrameRate", out HTuple actual);
                    System.Diagnostics.Debug.WriteLine($"FrameRate: yêu cầu {fps}, thực tế {actual}");
                }
                return true;
            }
            catch (HalconException ex)
            {
                OnError?.Invoke(this, $"Không thể chỉnh Frame Rate: {ex.Message}");
                return false;
            }
            finally
            {
                if (wasGrabbing) StartGrabbing();
            }
        }

        /// <summary>
        /// Chỉ gọi trong lock. Bỏ qua lỗi nếu tham số không tồn tại.
        /// </summary>
        private void TrySet(string name, HTuple value)
        {
            try { HOperatorSet.SetFramegrabberParam(_hAcqHandle, name, value); }
            catch (HalconException ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrySet bỏ qua '{name}'={value}: {ex.Message}");
            }
        }

        #endregion

        #region Trigger

        /// <summary>
        /// Cấu hình trigger. useTrigger = false thì chạy liên tục (TriggerMode Off).
        /// Camera này không liệt kê TriggerSource nên chỉ set nếu có.
        /// </summary>
        /// <summary>Thông báo lỗi gần nhất của thao tác Trigger (để VM hiển thị).</summary>
        public string LastError { get; private set; } = string.Empty;

        private string ReadParam(string name)
        {
            try
            {
                HOperatorSet.GetFramegrabberParam(_hAcqHandle, name, out HTuple v);
                return v.ToString();
            }
            catch (HalconException ex)
            {
                return $"(lỗi: {ex.Message})";
            }
        }

        public bool ConfigureTrigger(bool useTrigger, bool isSoftwareTrigger = true, string hardwareLine = "Line0")
        {
            bool wasGrabbing = _isGrabbing;
            StopGrabbing();   // dừng loop ngoài lock

            try
            {
                lock (_lockObj)
                {
                    if (_hAcqHandle == null)
                    {
                        LastError = "Camera chưa kết nối";
                        return false;
                    }

                    // Dừng acquisition ở tầng HALCON: GrabImageAsync đã tự bật acquisition,
                    // khi đang chạy camera khóa TriggerMode nên set sẽ lỗi / không có tác dụng.
                    // Chỉ cần khi bật trigger thật; ở chế độ Off camera đã chạy ổn nên bỏ qua.
                    if (useTrigger)
                        TrySet("do_abort_grab", 1);

                    string wanted = useTrigger ? "On" : "Off";
                    try
                    {
                        if (useTrigger)
                            TrySet("TriggerSource", isSoftwareTrigger ? "Software" : hardwareLine);

                        HOperatorSet.SetFramegrabberParam(_hAcqHandle, "TriggerMode", wanted);
                    }
                    catch (HalconException ex)
                    {
                        LastError = $"Set TriggerMode='{wanted}' lỗi: {ex.Message}. " +
                                    $"Giá trị hợp lệ: {ReadParam("TriggerMode_values")}";
                        OnError?.Invoke(this, LastError);
                        return false;
                    }

                    // Đọc lại để kiểm tra camera đã nhận chưa
                    string actual = ReadParam("TriggerMode");
                    System.Diagnostics.Debug.WriteLine($"TriggerMode: yêu cầu {wanted}, thực tế {actual}");

                    if (!string.Equals(actual.Trim('"'), wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        LastError = $"TriggerMode yêu cầu '{wanted}' nhưng camera trả về '{actual}'. " +
                                    $"Giá trị hợp lệ: {ReadParam("TriggerMode_values")}";
                        OnError?.Invoke(this, LastError);
                        return false;
                    }
                }

                LastError = string.Empty;
                return true;
            }
            finally
            {
                if (wasGrabbing) StartGrabbing();
            }
        }

        /// <summary>
        /// Hiển thị các thông số liên quan Trigger của camera để chẩn đoán.
        /// </summary>
        public void DebugTrigger()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null)
                {
                    MessageBox.Show("Camera chưa được kết nối!");
                    return;
                }

                MessageBox.Show(
                    $"TriggerMode={ReadParam("TriggerMode")}\n" +
                    $"TriggerMode_values={ReadParam("TriggerMode_values")}\n" +
                    $"TriggerActivation={ReadParam("TriggerActivation")}\n" +
                    $"TriggerActivation_values={ReadParam("TriggerActivation_values")}\n" +
                    $"TriggerSource={ReadParam("TriggerSource")}\n" +
                    $"TriggerSource_values={ReadParam("TriggerSource_values")}\n" +
                    $"AcquisitionMode={ReadParam("AcquisitionMode")}\n" +
                    $"AcquisitionMode_values={ReadParam("AcquisitionMode_values")}\n" +
                    $"TriggerSoftware={ReadParam("TriggerSoftware")}\n" +
                    $"TriggerDelay={ReadParam("TriggerDelay")}",
                    "Trigger debug");
            }
        }

        /// <summary>
        /// Bắn 1 xung Software Trigger
        /// </summary>
        public bool ExecuteSoftwareTrigger()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null) return false;
                try
                {
                    HOperatorSet.SetFramegrabberParam(_hAcqHandle, "TriggerSoftware", "Execute");
                    return true;
                }
                catch (HalconException)
                {
                    // Một số camera/HALCON version nhận giá trị số cho node Command
                    try
                    {
                        HOperatorSet.SetFramegrabberParam(_hAcqHandle, "TriggerSoftware", 1);
                        return true;
                    }
                    catch (HalconException ex2)
                    {
                        LastError = $"Gửi TriggerSoftware lỗi: {ex2.Message}";
                        OnError?.Invoke(this, LastError);
                        return false;
                    }
                }
            }
        }

        #endregion

        #region Debug

        public void PrintSupportedParameters()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null)
                {
                    MessageBox.Show("Camera chưa được kết nối!");
                    return;
                }

                try
                {
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "available_param_names", out HTuple deviceParams);

                    // Danh sách có rất nhiều tên lặp (StreamThreadPriority...), lọc trùng
                    var names = new List<string>();
                    for (int i = 0; i < deviceParams.Length; i++)
                        names.Add(deviceParams[i].S);

                    string allParams = string.Join("\n", names.Distinct());
                    MessageBox.Show(allParams, "GenICam Device Parameters");
                }
                catch (HalconException ex)
                {
                    MessageBox.Show($"Lỗi truy vấn: {ex.Message}");
                }
            }
        }

        public void DebugGamma()
        {
            lock (_lockObj)
            {
                if (_hAcqHandle == null)
                {
                    MessageBox.Show("Camera chưa được kết nối!");
                    return;
                }

                try
                {
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "Gamma", out HTuple g0);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "Gamma_range", out HTuple range);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "ExposureAuto", out HTuple ea);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "PixelFormat", out HTuple pf);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "Gain_range", out HTuple gr);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "Contrast_range", out HTuple cr);
                    HOperatorSet.GetFramegrabberParam(_hAcqHandle, "ExposureTime_range", out HTuple er);

                    MessageBox.Show(
                        $"Gamma={g0}, range={range}\n" +
                        $"ExposureAuto={ea}, PixelFormat={pf}\n" +
                        $"Gain_range={gr}\n" +
                        $"Contrast_range={cr}\n" +
                        $"ExposureTime_range={er}");
                }
                catch (HalconException ex)
                {
                    MessageBox.Show($"Lỗi truy vấn: {ex.Message}");
                }
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Disconnect();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}