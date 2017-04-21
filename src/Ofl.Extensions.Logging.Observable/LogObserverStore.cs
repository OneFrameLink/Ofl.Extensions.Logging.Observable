using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Threading;

namespace Ofl.Extensions.Logging.Observable
{
    public class LogObserverStore : ILogObserverStore
    {
        #region Static state.

        private static readonly AsyncLocal<IImmutableDictionary<Guid, IObserver<LogEntry>>> Observers = new AsyncLocal<IImmutableDictionary<Guid, IObserver<LogEntry>>>();

        #endregion

        #region Implementation of IObservableStore

        public IEnumerator<IObserver<LogEntry>> GetEnumerator() => Observers?.Value?.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IDisposable Add(IObserver<LogEntry> observer)
        {
            // Validate parameters.
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            // Get the immutable dictionary.
            IImmutableDictionary<Guid, IObserver<LogEntry>> dictionary = Observers.Value ?? 
                ImmutableDictionary<Guid, IObserver<LogEntry>>.Empty;

            // Create the key.
            Guid key = Guid.NewGuid();

            // Add the item.
            dictionary = dictionary.Add(key, observer);

            // Add back to the dictionary.
            Observers.Value = dictionary;

            // It's added.  Create the disposable to remove it.
            return Disposable.Create(() => Remove(key));
        }

        private void Remove(Guid key)
        {
            // Get the dictionary.
            IImmutableDictionary<Guid, IObserver<LogEntry>> dictionary = Observers.Value;

            // Remove.
            dictionary = dictionary.Remove(key);

            // Set the dictionary.
            Observers.Value = dictionary;

            // No point in sending OnCompleted, this can only be triggered after
            // an unsubscription, so that signal will never reach a listener.
        }

        #endregion
    }
}
