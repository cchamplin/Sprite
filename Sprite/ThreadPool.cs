using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sprite
{

    /// <summary>
    /// Thread pool class, creates a collection of threads of a fixed size
    /// and assigns a worker to each thread
    /// </summary>
    public class ThreadPool
    {
        private int _poolSize;
        private Thread[] _threads;
        private TaskWorker[] _workers;
        private ConcurrentQueue<SpriteTask> _taskQueue;
        public ThreadPool(int poolSize)
        {
            _taskQueue = new ConcurrentQueue<SpriteTask>();
            _poolSize = poolSize;
            _threads = new Thread[_poolSize];
            _workers = new TaskWorker[_poolSize];
            for (int x = 0; x < _poolSize; x++)
            {
                _workers[x] = new TaskWorker(_taskQueue);
                _threads[x] = new Thread(_workers[x].Run);
            }
        }
        public int PoolSize
        {
            get
            {
                return _poolSize;
            }
        }
        public ConcurrentQueue<SpriteTask> TaskQueue
        {
            get
            {
                return _taskQueue;
            }

        }
        public void StartPool()
        {
            for (int x = 0; x < _poolSize; x++)
            {
                _threads[x].Start();
            }
        }
        public void StopPool()
        {
            for (int x = 0; x < _poolSize; x++)
            {
                _workers[x].Stop();
            }
        }
    }
}
