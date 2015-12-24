namespace Altus.Suffūz.Observables
{
    public interface IPublisher
    {
        void Publish<T>(Disposed<T> disposed) where T : class, new();
        void Publish<T, U>(MethodCall<T, U> called) where T : class, new();
        void Publish<T>(Created<T> created) where T : class, new();
        void Publish<T, U>(PropertyUpdate<T, U> update) where T : class, new();
    }
}