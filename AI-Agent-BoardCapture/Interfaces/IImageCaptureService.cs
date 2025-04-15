using System;
using System.Threading.Tasks;

namespace ClassroomBoardCapture.Services
{
    /// <summary>
    /// Interface for image capture service
    /// </summary>
    public interface IImageCaptureService
    {
        /// <summary>
        /// Start capturing images at the configured interval
        /// </summary>
        /// <param name="waitForStopCallback">Callback to wait for stop signal</param>
        /// <returns>Task representing the capture process</returns>
        Task StartCaptureAsync(Func<System.Threading.CancellationToken, Task> waitForStopCallback);
    }
}