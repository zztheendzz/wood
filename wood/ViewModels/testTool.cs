using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using wood.Tools;

namespace wood.ViewModels
{
    public  class testTool : INotifyPropertyChanged
    {
        private HImage? _image;

        private double _minGray = 200;

        private double _maxGray = 220;

        private string _status = "Ready";

        private string _objectCount = "-";

        private string _totalArea = "-";


        public double MinGray
        {
            get => _minGray;
            set
            {
                _minGray = value;
                OnPropertyChanged();
            }
        }


        public double MaxGray
        {
            get => _maxGray;
            set
            {
                _maxGray = value;
                OnPropertyChanged();
            }
        }


        public string Status
        {
            get => _status;
            private set
            {
                _status = value;
                OnPropertyChanged();
            }
        }


        public string ObjectCount
        {
            get => _objectCount;
            private set
            {
                _objectCount = value;
                OnPropertyChanged();
            }
        }


        public string TotalArea
        {
            get => _totalArea;
            private set
            {
                _totalArea = value;
                OnPropertyChanged();
            }
        }


        public HImage? Image => _image;
        string imagePath = @"D:\destop\b2.jpg";
       

        public void SetImage(HImage image)
        {
            _image?.Dispose();
            _image = new HImage(imagePath);
            //_image = image;

            Status = "Image loaded";

            OnPropertyChanged(nameof(Image));
        }


        public VisionToolResult RunThreshold()
        {
            _image = new HImage(imagePath);
            if (_image == null)
            {
                return new VisionToolResult
                {
                    Success = false,
                    Message = "No image loaded."
                };
            }


            var tool = new ThresholdTool
            {
                Min = MinGray,
                Max = MaxGray
            };


            VisionToolResult result =
                tool.Run(_image);


            if (!result.Success)
            {
                Status = result.Message;

                return result;
            }


            // Lấy kết quả từ Tool

            if (result.Values.TryGetValue(
                    "Objects",
                    out string? objects))
            {
                ObjectCount = objects;
            }


            if (result.Values.TryGetValue(
                    "Total Area",
                    out string? area))
            {
                TotalArea = area;
            }


            Status = result.Message;

            return result;
        }


        public event PropertyChangedEventHandler?
            PropertyChanged;


        protected void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }

}

