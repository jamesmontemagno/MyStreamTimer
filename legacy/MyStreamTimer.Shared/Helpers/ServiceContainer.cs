using System;
using System.Collections.Generic;

namespace MyStreamTimer.Shared.Helpers
{
    public class ServiceContainer
    {
        static readonly object locker = new object();
        static ServiceContainer instance;

        ServiceContainer() => Services = new Dictionary<Type, Lazy<object>>();

        Dictionary<Type, Lazy<object>> Services { get; set; }

        static ServiceContainer Instance
        {
            get
            {
                lock (locker)
                {
                    if (instance == null)
                        instance = new ServiceContainer();
                    return instance;
                }
            }
        }

        public static void Register<T>(T service) => Instance.Services[typeof(T)] = new Lazy<object>(() => service);

        public static void Register<T>()
            where T : new() => Instance.Services[typeof(T)] = new Lazy<object>(() => new T());

        public static void Register<T>(Func<object> function) => Instance.Services[typeof(T)] = new Lazy<object>(function);

        public static T Resolve<T>()
        {
            if (Instance.Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service.Value;
            }

            return default(T);
        }
    }
}
