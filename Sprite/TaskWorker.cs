using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace Sprite
{
    /// <summary>
    /// Worker class which will look through a task queue
    /// and attempt to execute each task.
    /// </summary>
    internal class TaskWorker
    {

        /// <summary>
        /// Reference to task queue
        /// </summary>
        private ConcurrentQueue<SpriteTask> _taskQueue;

        /// <summary>
        /// Running state
        /// </summary>
        private Volatile.PaddedBoolean running;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="queue">Reference to task queue</param>
        public TaskWorker(ConcurrentQueue<SpriteTask> queue)
        {
            _taskQueue = queue;
            running = new Volatile.PaddedBoolean(true);
        }

        /// <summary>
        /// Cycles through the task queue and attempts to execute any task
        /// if no tasks are available the method will spin
        /// </summary>
        public void Run()
        {
            SpriteTask spriteTask; 
            // Loop forever, or until the process is stopped
            while (running.ReadFullFence())
            {

                // Attempt to dequeue a task
                if (_taskQueue.TryDequeue(out spriteTask) == false)
                {
                    // If not task is available spin once
                    default(SpinWait).SpinOnce();
                }
                else
                {
                    // Execute the task
                    spriteTask.ExecuteTask();
                }
            }
        }

        /// <summary>
        /// Set the running state to false
        /// </summary>
        public void Stop()
        {
            running.WriteFullFence(false);
        }
       
    }
}
