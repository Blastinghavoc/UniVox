using System.Collections.Generic;

namespace Utils.Pooling
{
    public class ObjectPool<T>
    {
        private Stack<T> stack;

        public int Count { get => stack.Count; }

        public ObjectPool()
        {
            stack = new Stack<T>();
        }

        public void Add(T obj)
        {
            stack.Push(obj);
        }

        public T Next()
        {
            return stack.Pop();
        }

    }
}