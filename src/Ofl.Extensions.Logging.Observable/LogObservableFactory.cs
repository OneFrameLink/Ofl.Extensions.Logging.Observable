using System;

namespace Ofl.Extensions.Logging.Observable
{
    public class LogObservableFactory : ILogObservableFactory
    {
        #region Constructor.

        public LogObservableFactory(ILogObserverStore logObserverStore)
        {
            // Validate parameters.
            _logObserverStore = logObserverStore ?? throw new ArgumentNullException(nameof(logObserverStore));
        }

        #endregion

        #region Instance, read-only state.

        private readonly ILogObserverStore _logObserverStore;

        #endregion

        #region ILogObservableFactory implementation.

        public IObservable<LogEntry> Create()
        {
            // We don't want to add to the observables
            // until subscribe is called.
            // To that end, use defer.
            return System.Reactive.Linq.Observable.Defer(CreateObservable);
        }

        private IObservable<LogEntry> CreateObservable()
        {
            // Call create.
            return System.Reactive.Linq.Observable.Create((Func<IObserver<LogEntry>, IDisposable>) CreateObservable);
        }

        private IDisposable CreateObservable(IObserver<LogEntry> observer)
        {
            // Validate parameters.
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            // Add to the current store and return.
            return _logObserverStore.Add(observer);
        }

        #endregion
    }
}
