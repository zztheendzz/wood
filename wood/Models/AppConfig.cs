using System;
using System.Collections.Generic;
using System.Text;

namespace wood.Models
{
    public class AppConfig
    {
        // COM Port
        public string SelectedComPort { get; set; } = "COM1";
        public int SelectedBaudRate { get; set; } = 9600;
        public string ComTriggerSignal { get; set; } = "1";
        public string ComResponseXop { get; set; } = "1";
        public string ComResponseGo { get; set; } = "2";
        public string ComResponseError { get; set; } = "0";

        // Camera
        public string SelectedCameraDevice { get; set; } = string.Empty;
        public double CamExposure { get; set; } = 5000;
        public double CamGain { get; set; } = 4.0;
        public double CamFrameRate { get; set; } = 30.0;
        public double CamGamma { get; set; } = 80;
        public double CamContrast { get; set; } = 100;

        // Thresh & Vision Params
        public double ThreshGrayMean { get; set; } = 200.0;
        public double ThreshGrayDev { get; set; } = 15.0;
        public double ThreshSatMean { get; set; } = 15.0;
        public double ThreshSatDev { get; set; } = 10.0;
        public double ThreshTextureMean { get; set; } = 30.0;

        public bool UseGrayMean { get; set; } = true;
        public bool UseGrayDev { get; set; } = true;
        public bool UseSatMean { get; set; } = true;
        public bool UseSatDev { get; set; } = true;
        public bool UseTextureMean { get; set; } = true;

        public string OperatorGrayMean { get; set; } = ">";
        public string OperatorGrayDev { get; set; } = "<";
        public string OperatorSatMean { get; set; } = "<";
        public string OperatorSatDev { get; set; } = "<";
        public string OperatorTextureMean { get; set; } = "<";

        public bool UseReceiveEndChar { get; set; }
        public string ReceiveEndChar { get; set; }

        public double AutoTimerIntervalSeconds { get; set; }

        public bool TriggerOnAnyScan {  get; set; }
        // Path
        public string SaveFolderPath { get; set; } = @"D:\Images\Processed";
    }
}
