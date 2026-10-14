using System;
using System.IO.Ports;
using System.Text;

namespace wood.Services.Communication.Com
{
    public class Com : IDisposable
    {
        private SerialPort _serialPort;

        // Sự kiện gửi dữ liệu RAW vừa nhận được (chưa qua xử lý, để VM tự buffer)
        public event Action<string> DataReceived;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public void Connect(string portName, int baudRate = 9600)
        {
            if (IsConnected)
            {
                Disconnect();
            }

            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Encoding = Encoding.UTF8,
                ReadTimeout = 2000,
                WriteTimeout = 1000
            };

            _serialPort.DataReceived += SerialPort_DataReceived;

            _serialPort.Open();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                // Đọc toàn bộ dữ liệu thô đang có trong bộ đệm của COM
                string chunk = _serialPort.ReadExisting();

                if (!string.IsNullOrEmpty(chunk))
                {
                    // Bắn thẳng dữ liệu thô ra ngoài để VM.cs tự gom (buffer) và cắt theo End Char tùy biến
                    DataReceived?.Invoke(chunk);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi nhận dữ liệu COM: {ex.Message}");
            }
        }

        public void Send(string data)
        {
            if (!IsConnected)
            {
                Console.WriteLine("COM chưa kết nối hoặc chưa mở");
                return;
            }

            try
            {
                // Gửi NGUYÊN BẢN chuỗi được truyền vào
                // (Vì bên VM.cs đã gọi hàm AppendLineEnding để thêm tùy biến \r, \n hoặc \r\n rồi)
                _serialPort.Write(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi dữ liệu: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}