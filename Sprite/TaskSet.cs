using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace Sprite
{
    public interface INotifiable
    {
        void NotifyCompleted();
        void NotifyException(Exception ex);
        bool Invalid();
    }
    /// <summary>
    ///  Container class for a collection of sprite tasks
    /// </summary>
    public class TaskSet : INotifiable
    {
        /// <summary>
        /// Current task queue
        /// </summary>
        ConcurrentQueue<SpriteTask> _taskQueue;

        /// <summary>
        ///  Total count tasks added to the set
        /// </summary>
        private Volatile.PaddedLong _taskCount;

        /// <summary>
        ///  Total count of tasks that have completed
        /// </summary>
        private Volatile.PaddedLong _tasksHandled;

        /// <summary>
        ///  Boolean if an error has occured in a task
        /// </summary>
        private Volatile.PaddedBoolean _taskError;

        /// <summary>
        /// Related exception for task errors
        /// </summary>

        private Exception _taskException;

        /// <summary>
        ///  Task ID
        /// </summary>
        private Guid _taskID;

        /// <summary>
        ///  Flag to denote whether or not the process has been interrupted
        /// </summary>
        private Volatile.PaddedBoolean _interrupt;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="taskQueue">Reference to the task queue</param>
        public TaskSet(ConcurrentQueue<SpriteTask> taskQueue)
        {
            _taskQueue = taskQueue;
            _taskID = Guid.NewGuid();
            _interrupt = new Volatile.PaddedBoolean(false);
            _taskError = new Volatile.PaddedBoolean(false);
        }

        /// <summary>
        /// Return the taskID
        /// </summary>
        public Guid TaskID
        {
            get
            {
                return _taskID;
            }
        }

        /// <summary>
        /// Create an additional task and add it to the queue
        /// </summary>
        /// <param name="executor">The task executor</param>
        /// <param name="resourceSet">The associated resource pool</param>
        /// <returns></returns>
        public SpriteTask EnqueTask(IRunnable executor, ConcurrentQueue<object> resourceSet)
        {
            // Ensure we have not experience any errors
            if (ErrorState() == false)
            {
                // Initialize a new task, utilizing this object as the notifiable
                var spriteTask = new SpriteTask(executor, resourceSet, this);

                // Increment the task count first
                _taskCount.AtomicIncrementAndGet();

                // Queue the newly created task
                _taskQueue.Enqueue(spriteTask);

                // Return the task
                return spriteTask;
            }
            else
            {
                // Throw the error
                throw _taskException;
            }
        }

        /// <summary>
        /// Call back for task completion
        /// </summary>
        public void NotifyCompleted()
        {
            _tasksHandled.AtomicIncrementAndGet();
        }

        /// <summary>
        ///  Returns whether or not all tasks have been processed.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                return (_tasksHandled.ReadFullFence() == _taskCount.ReadFullFence());
            }
        }

        /// <summary>
        /// Set the execution state to interrupted
        /// </summary>
        public void Interrupt()
        {
            _interrupt.WriteFullFence(true);
        }

        /// <summary>
        /// Blocking method which will spin until all tasks have been completed or the
        /// set has been interrupted
        /// </summary>
        public void WaitComplete()
        {
            
            while (!IsComplete && _interrupt.ReadFullFence() == false)
            {
                default(SpinWait).SpinOnce();
            }
        }

        /// <summary>
        /// Returns Set State
        /// </summary>
        /// <returns></returns>
        public bool Invalid()
        {
            return ErrorState();
        }


        /// <summary>
        /// Returns whether or not any errors have occured
        /// </summary>
        /// <returns></returns>
        public bool ErrorState()
        {
            return _taskError.ReadFullFence();
        }

        /// <summary>
        /// Returns exception reference if an error occurs
        /// </summary>
        /// <returns></returns>
        public Exception Error()
        {
            return _taskException;
        }


        /// <summary>
        /// Call back for task exceptions
        /// </summary>
        /// <param name="ex"></param>
        public void NotifyException(Exception ex)
        {
            _taskException = ex;
            _taskError.WriteFullFence(true);
        }
    }
}
