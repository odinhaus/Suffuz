using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class ILObservableBuilder : IObservableBuilder
    {
        static Dictionary<Type, Func<object, string, IPublisher, object>> _ilTypeBuilders = new Dictionary<Type, Func<object, string, IPublisher, object>>();

        public ILObservableBuilder()
        {
        }

        /// <summary>
        /// Creates a new type that wraps the base type, and provides hoooks into the eventing/synchronization framework when 
        /// properties are changed and methods are called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="globalKey"></param>
        /// <returns></returns>
        public T Create<T>(T instance, string globalKey, IPublisher publisher) where T : class, new()
        {
            Func<object, string, IPublisher, object> ilTypeBuilder;
            lock(_ilTypeBuilders)
            {
                if (!_ilTypeBuilders.TryGetValue(typeof(T), out ilTypeBuilder))
                {
                    var ilType = BuildType<T>();
                    ilTypeBuilder = BuildCreator(ilType);
                    _ilTypeBuilders.Add(typeof(T), ilTypeBuilder);
                }
            }
            return (T)ilTypeBuilder(instance, globalKey, publisher);
        }

        /// <summary>
        /// Creates a new type that wraps the base type, and provides hoooks into the eventing/synchronization framework when 
        /// properties are changed and methods are called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private Type BuildType<T>() where T : class, new()
        {
            var typeBuilder = new ILObservableTypeBuilder();
            return typeBuilder.Build(typeof(T));
        }

        /// <summary>
        /// Creates a typed activator for the new IL type
        /// </summary>
        /// <param name="ilType"></param>
        /// <returns></returns>
        private Func<object, string, IPublisher, object> BuildCreator(Type ilType)
        {
            var instanceParam = Expression.Parameter(typeof(object));
            var globalKeyParam = Expression.Parameter(typeof(string));
            var publisherParam = Expression.Parameter(typeof(IPublisher));
            var ctor = Expression.New(ilType.GetConstructors().Single(c => c.GetParameters().Length > 0),
                publisherParam,
                Expression.Convert(instanceParam, ilType.BaseType),
                globalKeyParam);
            var lambda = Expression.Lambda<Func<object, string, IPublisher, object>>(ctor, instanceParam, globalKeyParam, publisherParam);
            return lambda.Compile();
        }
    }
}
