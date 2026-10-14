using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nodify;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Models;
using wood.Tools;
using Connection = wood.Models.Connection;
using System.Windows;

namespace wood.ViewModels
{
    public partial class VisionVM : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ToolItem> tools = new();

        [ObservableProperty]
        private ObservableCollection<Connection>? connections = new();

        [ObservableProperty]
        private ObservableCollection<Node>? nodes = new();

        [ObservableProperty]
        private ObservableCollection<Ports>? ports = new();

        [ObservableProperty]
        private ReadImageTool? readImage;

        [ObservableProperty]
        private ToGrayTool? toGrayTool;

        [ObservableProperty]
        private ThresholdTool? thresholdTool;

        [ObservableProperty]
        private BlobTool? blobTool;

        [ObservableProperty]
        private Region? regionSelectTool;

        [RelayCommand]
        private void CreateConnect()
        {
            // Logic xử lý khi nhấn nút Save
        }


        [RelayCommand]
        private void CreateNode()
        {
            // Logic xử lý khi nhấn nút Save
        }
        public void AddNode(
            string toolType,
           System.Windows.Point location)
        {
            //Nodes.Add(new Nodes
            //{
            //    Title = toolType,
            //    Location = location
            //});
        }

    }
}
