using System;
using System.Collections;
using System.Threading.Tasks;

namespace AmpPortableDataViz.Presentation.Utility
{
    internal static class TaskExtensions
    {
        public static IEnumerator AsIEnumerator(this Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted && task.Exception != null) throw task.Exception;
        }

        public static IEnumerator AsIEnumerator<TResult>(this Task<TResult> task, Action<TResult> onComplete = null)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted && task.Exception != null) throw task.Exception;
            onComplete?.Invoke(task.Result);
        }
    }
}

