using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wood.Models
{
    public class Connection 
    {
        // ID duy nhất của Connection
        public Guid Id { get; set; } = Guid.NewGuid();

        // Node nguồn
        public Guid SourceNodeId { get; set; }

        // Output Port của Node nguồn
        public Guid SourcePortId { get; set; }

        // Node đích
        public Guid TargetNodeId { get; set; }

        // Input Port của Node đích
        public Guid TargetPortId { get; set; }

        public Connection() { }               
        public Connection(Guid SourceNodeId, Guid SourcePortId, Guid TargetNodeId,Guid TargetPortId) {
        
            this.SourceNodeId = SourceNodeId;
            this.SourcePortId = SourcePortId;
            this.TargetNodeId = TargetNodeId;
            this.TargetPortId = TargetPortId;
        }



    }
}
