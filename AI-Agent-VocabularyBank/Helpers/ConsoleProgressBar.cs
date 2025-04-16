using System;
using System.Text;
using System.Threading;

namespace VocabularyBank.Helpers
{
    /// <summary>
    /// Helper class to display a progress bar in the console.
    /// </summary>
    public class ConsoleProgressBar : IDisposable
    {
        private const int ProgressBarWidth = 40;
        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(0.1);
        private readonly Timer _timer;
        private bool _disposed = false;
        private int _currentProgress = 0;
        private string _currentMessage = string.Empty;
        private readonly object _syncLock = new object();
        private readonly StringBuilder _progressBarText = new StringBuilder();
        private readonly int _initialCursorTop;
        private bool _isFinished;

        /// <summary>
        /// Initializes a new progress bar in the console.
        /// </summary>
        /// <param name="initialMessage">Initial message to display with the progress bar</param>
        public ConsoleProgressBar(string initialMessage = "")
        {
            _currentMessage = initialMessage;
            _initialCursorTop = Console.CursorTop;
            
            // Don't display cursor while we're rendering the progress bar
            Console.CursorVisible = false;
            
            // Draw initial progress bar (0%)
            RenderProgressBar();

            // Set up timer for animation
            _timer = new Timer(TimerHandler, null, _animationInterval, _animationInterval);
        }

        /// <summary>
        /// Updates the progress bar with a new percentage value.
        /// </summary>
        /// <param name="percentComplete">Percentage complete (0-100)</param>
        /// <param name="message">Optional message to display</param>
        public void Report(int percentComplete, string message = null)
        {
            // Clamp value to valid range
            percentComplete = Math.Max(0, Math.Min(100, percentComplete));
            
            lock (_syncLock)
            {
                _currentProgress = percentComplete;
                if (message != null)
                {
                    _currentMessage = message;
                }
                
                // If we're at 100%, mark as finished
                if (percentComplete >= 100)
                {
                    _isFinished = true;
                }
            }
        }
        
        /// <summary>
        /// Marks the progress bar as complete.
        /// </summary>
        public void Complete()
        {
            lock (_syncLock)
            {
                _currentProgress = 100;
                _isFinished = true;
            }
            
            // Give the timer a chance to render the final state
            Thread.Sleep(150);
            
            // Clean up
            Dispose();
            
            // Move to the next line
            Console.WriteLine();
        }

        /// <summary>
        /// Timer callback that renders the progress bar.
        /// </summary>
        private void TimerHandler(object state)
        {
            lock (_syncLock)
            {
                RenderProgressBar();
                
                // If we're finished, dispose of the timer
                if (_isFinished)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Renders the current state of the progress bar.
        /// </summary>
        private void RenderProgressBar()
        {
            // Save the current cursor position
            int originalCursorLeft = Console.CursorLeft;
            int originalCursorTop = Console.CursorTop;
            
            // Move cursor to where we want to draw the progress bar
            Console.SetCursorPosition(0, _initialCursorTop);
            
            // Clear the progress bar line
            _progressBarText.Clear();
            
            // Build progress bar: [==========>          ] 50%
            _progressBarText.Append("[");
            
            int completedWidth = (int)(_currentProgress / 100.0 * ProgressBarWidth);
            
            for (int i = 0; i < ProgressBarWidth; i++)
            {
                if (i < completedWidth)
                    _progressBarText.Append("█");
                else if (i == completedWidth)
                    _progressBarText.Append("▓");
                else
                    _progressBarText.Append("░");
            }
            
            _progressBarText.Append("] ");
            _progressBarText.Append(_currentProgress.ToString().PadLeft(3));
            _progressBarText.Append("% ");
            _progressBarText.Append(_currentMessage);
            
            // Fill the rest of the line with spaces to clear old text
            int remainingSpace = Math.Max(0, Console.WindowWidth - _progressBarText.Length);
            _progressBarText.Append(new string(' ', remainingSpace));
            
            // Write the progress bar
            Console.Write(_progressBarText);
            
            // Restore the cursor position
            Console.SetCursorPosition(originalCursorLeft, originalCursorTop);
        }

        /// <summary>
        /// Disposes of the progress bar, cleaning up resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _timer?.Dispose();
                // Show cursor again
                Console.CursorVisible = true;
                _disposed = true;
            }
        }
    }
}
