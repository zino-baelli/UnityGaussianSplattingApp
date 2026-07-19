using System;
using System.Collections.Generic;
using UnityEngine;

namespace SplattingPipeline
{
    /// <summary>
    /// Drop this on one GameObject in your scene (or let it self-instantiate).
    /// Anything called from ProcessRunner's events (background thread) that needs
    /// to touch UI must go through Enqueue(), not be called directly.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly object _lock = new object();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject(nameof(MainThreadDispatcher));
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public void Enqueue(Action action)
        {
            lock (_lock) { _queue.Enqueue(action); }
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_queue.Count > 0)
                    _queue.Dequeue().Invoke();
            }
        }
    }
}
