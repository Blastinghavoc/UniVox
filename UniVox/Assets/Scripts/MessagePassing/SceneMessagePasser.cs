using System;
using System.Collections.Generic;

namespace UniVox.MessagePassing
{
    /// <summary>
    /// Facilitates message passing between scenes
    /// </summary>
    public class SceneMessagePasser
    {
        private static Dictionary<Type, object> messages;

        public static void SetMessage<T>(T message)
        {
            if (messages == null)
            {
                messages = new Dictionary<Type, object>();
            }
            messages.Add(typeof(T), message);
        }

        public static void RemoveMessage<T>()
        {
            if (messages != null)
            {
                messages.Remove(typeof(T));
            }
        }

        public static bool TryConsumeMessage<T>(out T message)
        {
            var result = TryGetMessage<T>(out message);
            if (result)
            {
                RemoveMessage<T>();
            }
            return result;
        }
        public static bool TryGetMessage<T>(out T message)
        {
            if (messages == null)
            {
                message = default;
                return false;
            }

            bool result = messages.TryGetValue(typeof(T), out var tmp);
            if (result)
            {
                message = (T)tmp;
            }
            else
            {
                message = default;
            }
            return result;
        }
    }
}