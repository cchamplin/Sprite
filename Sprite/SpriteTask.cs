using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace Sprite
{
    public interface IRunnable
    {
        object HandleData(object lastRecord, object data, object resource);
    }
    /// <summary>
    /// A individual take with dispatches work to an executor based on a finite resource set
    /// </summary>
    public class SpriteTask
    {
        /// <summary>
        /// Denotes if an error has occured during the execution of the IRunnable
        /// </summary>
        private Volatile.PaddedBoolean _errorState;

        /// <summary>
        /// Exception associated with any errors
        /// </summary>
        private Exception _errorException;

        /// <summary>
        /// Denotes that the task is complete and no more work can/will be received
        /// </summary>
        private Volatile.PaddedBoolean _taskComplete;

        /// <summary>
        /// Determines whether or not the task is running
        /// </summary>
        private Volatile.PaddedBoolean _running;

        /// <summary>
        /// Total data queued count
        /// </summary>
        private Volatile.PaddedLong _taskDataCount;

        /// <summary>
        /// Total data handled count
        /// </summary>
        private Volatile.PaddedLong _taskDataHandled;

        /// <summary>
        /// Holds queued task data, waiting to be dispatched to the executor
        /// </summary>
        private ConcurrentQueue<object> _taskData;

        /// <summary>
        /// Reference to a resource queue
        /// </summary>
        private ConcurrentQueue<object> _resourceSet;

        /// <summary>
        /// Executor which will receive work whenever a work and a resource are available
        /// </summary>
        private IRunnable _executor;

        /// <summary>
        /// Notifier to receive notication on work updates/errors
        /// </summary>
        private INotifiable _notifier;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="executor">Work will be dispatched to the executor</param>
        /// <param name="resourceSet">Finite resource set which will limit the work dispatching</param>
        /// <param name="notifier">Notifiable to receive updates on task status changes</param>
        public SpriteTask(IRunnable executor, ConcurrentQueue<object> resourceSet, INotifiable notifier)
        {
            _executor = executor;
            _resourceSet = resourceSet;
            _notifier = notifier;

            _taskData = new ConcurrentQueue<object>();


            _taskComplete = new Volatile.PaddedBoolean(false);
            _errorState = new Volatile.PaddedBoolean(false);
            _running = new Volatile.PaddedBoolean(true);
            _taskDataHandled = new Volatile.PaddedLong(0);
            _taskDataCount = new Volatile.PaddedLong(0);

        }
        /// <summary>
        /// Adds data to the execution queue
        /// </summary>
        /// <param name="data">Object to be passed to the executor</param>
        public void AddData(object data)
        {
            // A completed task cannot have data added to it
            if (_taskComplete.ReadFullFence())
                throw new InvalidOperationException("Attempted to add data to an object marked as complete");

            // An invalid task cannot have data added to it
            if (_errorState.ReadFullFence())
                throw new InvalidOperationException("Task Invalid, an error has occured");

            // Increment the counter of total queued work
            // Order matters here, we need to increment the counter first
            // so that we avoid race conditions in which the work could be processed
            // before it's added to the count which would potentially put the task into
            // and invalid state
            _taskDataCount.AtomicIncrementAndGet();

            // Queue the work
            _taskData.Enqueue(data);
        }

        /// <summary>
        /// Returns true if the task has been marked as complete externally
        /// meaning no more data should be received
        /// </summary>
        private bool IsComplete
        {
            get
            {
                return _taskComplete.ReadFullFence();
            }
        }
        

        /// <summary>
        /// Set the task as complete, the task will not expect any more data to enter its queue
        /// </summary>
        public void SetComplete()
        {
            _taskComplete.WriteFullFence(true);
        }

        /// <summary>
        /// Checks if the task is marked as complete and all pieces of data have been dispatched
        /// to the executor
        /// </summary>
        /// <returns></returns>
        private bool DataComplete()
        {
            return IsComplete && (_taskDataCount.ReadFullFence() == _taskDataHandled.ReadFullFence());
        }

        /// <summary>
        /// Returns true if the task has experienced an error processing data
        /// </summary>
        /// <returns></returns>
        public bool ErrorState()
        {
            return _errorState.ReadFullFence();
        }

        /// <summary>
        /// Will contain the exception reference should any errors occur
        /// </summary>
        /// <returns></returns>
        public Exception Error()
        {
            return _errorException;
        }

        /// <summary>
        /// A blocking method that will run until the task has been marked as complete
        /// and all data has been processed.
        /// </summary>
        public void ExecuteTask()
        {

            // Make sure that the notifier is in a valid state
            // otherwise send notification of completion
            // TODO Check this logic
            if (_notifier.Invalid())
            {
                _notifier.NotifyCompleted();
                return;
            }



            // This will store the piece of data to be dispatched to the executor
            object nextData;

            // Resource will be the active resource when one becomes available
            object resource;

            // Active record is meant to store a current working object for the handler should it need it
            object activeRecord = null;


            bool gotData = false;

            // Attempt get get a resource
            // Spin if one isn't available
            while (_resourceSet.TryDequeue(out resource) == false)
            {
                default(SpinWait).SpinOnce();
            }
            
            // Make sure we are still running then attempt to grab a piece of data from the queue
            // if no data is available we will spin unless the task is complete
            while (_running.ReadFullFence() && (((gotData = _taskData.TryDequeue(out nextData)) == true) || !DataComplete()))
            {
                // Ensure we have data or spin
                if (gotData)
                {

                    // Increment the total data handled counter
                    _taskDataHandled.AtomicIncrementAndGet();
                    try
                    {
                        // Call the data handled on the executor
                        // Pass in the current working object, the piece of data, and the resource
                        // take back out the current working object
                        activeRecord = _executor.HandleData(activeRecord, nextData, resource);
                    }
                    catch (Exception ex)
                    {
                        // If an exception occurs
                        // requeue the resource
                        // and set the appropriate error state information
                        _resourceSet.Enqueue(resource);
                        _errorException = ex;
                        _errorState.WriteFullFence(true);
                        _notifier.NotifyException(ex);
                        _notifier.NotifyCompleted();
                        return;
                    }


                }
                else
                {
                    default(SpinWait).SpinOnce();
                }
            }
            // We are data complete, requeue the resource and send notification
            _resourceSet.Enqueue(resource);
            _notifier.NotifyCompleted();
        }
    }
}
