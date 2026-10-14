using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using Microsoft.Win32;
using Nodify;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using wood.Interface;
namespace wood.Tools
{
    public class ReadImageTool : IVisionTool
    {


        public string Name => "Read Image";
        public string Description => "Load an image from file.";

        private HImage currentImage = new HImage() ;
        private string imagePath = string.Empty;
        //private VisionToolResult? currentResult;
         private string statusMessage = "Ready";

        public VisionToolResult Run(Object input)
        {
            string pathImage =(string) input ;
            if (pathImage != null)
            { this.imagePath = pathImage; }

            var dialog = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff" };
            if (dialog.ShowDialog() != true) return  new VisionToolResult(false, null, "type image is not correctly"); ;
            if (imagePath == string.Empty) {
                VisionToolResult result = new VisionToolResult(false,null, "image path is not correctly");
                return result;
            }
            try
            {
                currentImage?.Dispose();
                currentImage = new HImage(dialog.FileName);
                imagePath = dialog.FileName;
                statusMessage = "Image loaded";
                VisionToolResult result = new VisionToolResult(true, currentImage, statusMessage);
                return result;
            }
            catch (Exception ex) { 
                statusMessage = $"Load image failed: {ex.ToString()}"; 
                return new VisionToolResult(false, null, ex.ToString());
            }

        }
        public ReadImageTool() { }
        public ReadImageTool(string imagePath) {
        this.imagePath = imagePath;
        }

    }
}
