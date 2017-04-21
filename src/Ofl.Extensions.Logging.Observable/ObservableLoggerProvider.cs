using System;
using Microsoft.Extensions.Logging;

namespace Ofl.Extensions.Logging.Observable
{
    public class ObservableLoggerProvider : ILoggerProvider
    {
        #region Constructor

        public ObservableLoggerProvider(ILogObserverStore logObserverStore)
        {
            // Validate parameters.
            _logObserverStore = logObserverStore ?? throw new ArgumentNullException(nameof(logObserverStore));
        }

        #endregion

        #region Instance, read-only state.

        private readonly ILogObserverStore _logObserverStore;

        #endregion

        #region ILoggerProvider implementation.

        public ILogger CreateLogger(string categoryName)
        {
            // Validate parameters.
            if (string.IsNullOrWhiteSpace(categoryName)) throw new ArgumentNullException(nameof(categoryName));

            // Create a logger and return.
            return new ObservableLogger(categoryName, _logObserverStore);
        }

        public void Dispose()
        {
            // Nothing to do here.
        }


        #endregion
    }
}
