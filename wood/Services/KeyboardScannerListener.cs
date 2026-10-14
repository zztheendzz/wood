using System;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace wood.Services
{
    /// <summary>
    /// Nhận mã từ máy quét QR/barcode USB chạy chế độ "bàn phím" (HID keyboard wedge).
    /// Máy quét gõ cả chuỗi rất nhanh (vài ms/ký tự) rồi kết thúc bằng Enter (hoặc Tab),
    /// còn người gõ phím thì chậm hơn nhiều, nhờ đó phân biệt được hai trường hợp.
    /// Gắn vào cửa sổ chính (Window) để nhận phím khi cửa sổ đang được focus.
    /// </summary>
    public sealed class KeyboardScannerListener : IDisposable
    {
        private readonly UIElement _target;
        private readonly StringBuilder _buffer = new StringBuilder();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private bool _disposed;

        /// <summary>Khoảng cách tối đa giữa 2 ký tự để coi là máy quét (ms).</summary>
        public int MaxIntervalMs { get; set; } = 50;

        /// <summary>Độ dài tối thiểu của mã hợp lệ.</summary>
        public int MinLength { get; set; } = 3;

        /// <summary>Có coi phím Tab là ký tự kết thúc (một số máy quét cấu hình như vậy).</summary>
        public bool AcceptTabAsTerminator { get; set; } = true;

        /// <summary>Phát ra khi quét xong một mã.</summary>
        public event Action<string> CodeScanned;

        public KeyboardScannerListener(UIElement target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _target.PreviewTextInput += OnPreviewTextInput;
            _target.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            DateTime now = DateTime.UtcNow;

            // Gõ chậm (người) -> bỏ phần đã gom, bắt đầu lại
            if ((now - _lastKeyTime).TotalMilliseconds > MaxIntervalMs)
                _buffer.Clear();

            _lastKeyTime = now;
            _buffer.Append(e.Text);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isTerminator = e.Key == Key.Enter ||
                                (AcceptTabAsTerminator && e.Key == Key.Tab);
            if (!isTerminator) return;

            DateTime now = DateTime.UtcNow;
            bool fast = (now - _lastKeyTime).TotalMilliseconds <= MaxIntervalMs;

            if (_buffer.Length >= MinLength && fast)
            {
                string code = _buffer.ToString().Trim();
                _buffer.Clear();

                // Không cho Enter/Tab kích hoạt nút mặc định hay chuyển focus
                e.Handled = true;

                if (code.Length > 0)
                    CodeScanned?.Invoke(code);
            }
            else
            {
                _buffer.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _target.PreviewTextInput -= OnPreviewTextInput;
            _target.PreviewKeyDown -= OnPreviewKeyDown;
        }
    }
}