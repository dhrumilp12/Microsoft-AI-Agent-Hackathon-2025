using Microsoft.SemanticKernel;

namespace AI_Agent_Orchestrator.Services
{
    public class KernelService
    {
        private readonly Kernel _kernel;

        public KernelService()
        {
            _kernel = Kernel.CreateBuilder().Build();
        }

        public Kernel GetKernel() => _kernel;
    }
}