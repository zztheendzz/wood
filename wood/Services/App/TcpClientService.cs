using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace wood.Services.App
{
    public class TcpClientService
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly object _lock = new object();

        /// <summary>
        /// Sự kiện Trigger khi nhận được gói tin "0" kết thúc bằng \r\n
        /// </summary>
        public Action OnZeroTriggered { get; set; }

        /// <summary>
        /// Sự kiện nhận toàn bộ dữ liệu thô khác
        /// </summary>
        public Action<string> OnRawData { get; set; }

        public  async Task ConnectAsync(string ip, int port)
        {
            Disconnect();

            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // Server ngắt kết nối

                    string receivedChunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    lock (_lock)
                    {
                        _buffer.Append(receivedChunk);
                        ProcessBuffer();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP Receive Error]: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void ProcessBuffer()
        {
            while (true)
            {
                string currentContent = _buffer.ToString();

                // 1. Tìm ngắt chuỗi \r\n
                int indexOfDelim = currentContent.IndexOf("\r\n", StringComparison.Ordinal);
                if (indexOfDelim < 0) break; // Chưa nhận đủ gói tin hoàn chỉnh

                // 2. Tách lấy gói tin từ đầu đến trước \r\n
                string packet = currentContent.Substring(0, indexOfDelim);

                // Xóa gói tin + 2 ký tự \r\n khỏi bộ đệm
                _buffer.Remove(0, indexOfDelim + 2);

                // Loại bỏ dấu phẩy hoặc khoảng trắng thừa nếu có
                string cleanData = packet.Replace(",", "").Trim();

                // 3. Bắt điều kiện: Nếu gói tin là "0" -> Trigger sự kiện
                if (cleanData == "0")
                {
                    Console.WriteLine("[Trigger]: Nhận được gói tin '0'!");
                    OnZeroTriggered?.Invoke();
                }
                else
                {
                    // Các gói tin khác gửi ra event dữ liệu thông thường
                    OnRawData?.Invoke(cleanData);
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _stream?.Dispose();
                _client?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect Error]: {ex.Message}");
            }
            finally
            {
                _cts = null;
                _stream = null;
                _client = null;
            }
        }
    }
}