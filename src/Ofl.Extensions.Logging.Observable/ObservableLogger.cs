using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace Ofl.Extensions.Logging.Observable
{
    public class ObservableLogger : ILogger
    {
        #region Constructor

        internal ObservableLogger(
            string categoryName,
            ILogObserverStore logObserverStore)
        {
            // Validate parameters.
            if (string.IsNullOrWhiteSpace(categoryName)) throw new ArgumentNullException(nameof(categoryName));
            _logObserverStore = logObserverStore ?? throw new ArgumentNullException(nameof(logObserverStore));

            // Assign values.
            _categoryName = categoryName;
        }

        #endregion

        #region Instance, read-only state.

        private readonly string _categoryName;

        private readonly ILogObserverStore _logObserverStore;

        #endregion

        #region ILogger implementation.

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // Get the enumerator.
            using (IEnumerator<IObserver<LogEntry>> enumerator = _logObserverStore.GetEnumerator())
            {
                // If null, or there are no elements, get out.
                if (!(enumerator?.MoveNext() ?? false)) return;

                // Create the log entry.
                var logEntry = new LogEntry(
                    logLevel,
                    eventId,
                    _categoryName,
                    state,
                    exception,
                    formatter(state, exception)
                );

                // Iterate through the observers and fire OnNext.  
                // Logging should never throw an exception, so swallow here.
                // Already have one element, use that, then
                // check if we can move next.
                do
                {
                    // Wrap in a try/catch.
                    try
                    {
                        // Execute.
                        enumerator.Current.OnNext(logEntry);
                    }
                    catch
                    {
                        // Swallow for now.
                    }
                } while (enumerator.MoveNext());
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Everything enabled for now.
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            // Return a null log scope for now.
            // TODO: Figure out what to do here.
            return NullScope.Instance;
        }

        #endregion
    }
}
