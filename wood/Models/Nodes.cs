using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Interface;

namespace wood.Models
{
    public  class Nodes 
    {


        // ID duy nhất của Node
        public Guid Id { get; set; } = Guid.NewGuid();

        // Tên hiển thị trên UI
        public string Name { get; set; } = string.Empty;

        // Loại Node: ReadImage, Threshold, Blob...
        public string Type { get; set; } = string.Empty;

        // Port đầu vào
        public List<Ports> Inputs { get; set; } = new();

        // Port đầu ra
        public List<Ports> Outputs { get; set; } = new();

        public Point Location { get; set; }
        // Parameter của Tool
        public Dictionary<string, object?> Parameters { get; set; } = new();
        public IVisionTool? Tool { get; set; }
        public string Title { get; internal set; }

        public Type ToolType { get; set; }

        public Nodes() { }

    }
}
