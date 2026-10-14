using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wood.Models;

using HalconDotNet;
namespace wood.Services
{
    public class PipelineExecutor
    {
        public PipelineExecutor() { }
        public void Execute(Pipeline pipeline)
        {

            // Tìm node có thể chạy 
            // Chạy node
            // Đẩy output qua connection
            // Node downstream đủ input thì chạy
            // Tiếp tục cho đến khi hết node

            var context = new wood.Models.ExecutionContext();

            var executedNodes = new HashSet<Guid>();

            while (executedNodes.Count < pipeline.Nodes.Count)
            {
                bool executedSomething = false;

                foreach (var node in pipeline.Nodes)
                {
                    if (executedNodes.Contains(node.Id))
                        continue;

                    // Node đã đủ input chưa?
                    if (!CanExecute(node, pipeline, context))
                        continue;

                    // Chạy node
                    ExecuteNode(node, context);

                    executedNodes.Add(node.Id);

                    executedSomething = true;
                }

                // Không còn node nào chạy được 
                if (!executedSomething)
                {
                    throw new Exception(
                        "Pipeline không thể tiếp tục. Có thể thiếu input hoặc connection bị lỗi.");
                }
            }
        }
        private bool CanExecute(Nodes node, Pipeline pipeline, wood.Models.ExecutionContext context)
        {
            foreach (var input in node.Inputs)
            {
                // Tìm connection đi vào input này aac
                // Nếu không có connection node    
                var connection = pipeline.Connections
                    .FirstOrDefault(x =>
                        x.TargetNodeId == node.Id &&
                        x.TargetPortId == input.Id);

                // Input không có connection
                if (connection == null)
                {
                    return false;
                }

                // Output của node trước chưa có data 
                //                                    

                if (!context.HasValue(connection.SourcePortId))
                {
                    return false;
                }
            }
            return true;
        }
        private void ExecuteNode(Nodes node, wood.Models.ExecutionContext context)
        {
            var inputs = new Dictionary<string, object?>();

            foreach (var input in node.Inputs)
            {
                var value = context.Get(input.Id);

                inputs[input.Name] = value;
            }

            var result = node.Tool?.Run(inputs);

            if (!result.Success)
            {
                throw new Exception(
                    $"Node '{node.Name}' chạy thất bại: {result.Message}");
            }

            foreach (var output in node.Outputs)
            {
                if (result.Values.TryGetValue(output.Name, out var value))
                {
                    context.Set(output.Id, value);
                }
            }
        }
    }
}
