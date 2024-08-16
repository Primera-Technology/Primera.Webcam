using System;
using System.Threading;

using Optional;

namespace Primera.Webcam.Streaming
{
    /// <summary>
    /// Some objects exist on a thread and cannot be manipulated across that thread boundary.
    /// This class exposes an object and a synchronization context to that object that can be used to manipulate it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SynchronizedObject<T>
    {
        public SynchronizedObject(T obj, SynchronizationContext context)
        {
            Obj = obj;
            Context = context;
        }

        public SynchronizationContext Context { get; }

        public T Obj { get; }

        public static SynchronizedObject<T> Create(SynchronizationContext context, Func<T> constructor)
        {
            T obj = default;
            context.Send(_ => obj = constructor(), null);
            return new SynchronizedObject<T>(obj, context);
        }

        public static Option<SynchronizedObject<T>> CreateOption(SynchronizationContext context, Func<Option<T>> constructor)
        {
            Option<T> obj = Option.None<T>();
            context.Send(_ => obj = constructor(), null);
            return obj.Map(o => new SynchronizedObject<T>(o, context));
        }

        public SynchronizedObject<T2> Map<T2>(Func<T, T2> func)
        {
            T2 result = default;
            Context.Send(obj =>
            {
                result = func((T)obj);
            }, Obj);
            return new SynchronizedObject<T2>(result, Context);
        }

        public void Post(Action<T> action)
        {
            Context.Post(obj => action((T)obj), Obj);
        }

        public void Send(Action<T> action)
        {
            Context.Send(obj => action((T)obj), Obj);
        }
    }
}