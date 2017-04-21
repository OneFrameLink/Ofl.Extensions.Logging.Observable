using System;
using Microsoft.Extensions.Logging;

namespace Ofl.Extensions.Logging.Observable
{
    public static class LoggerFactoryExtensions
    {
        public static ILoggerFactory AddObservable(this ILoggerFactory loggerFactory,
            ILogObserverStore logObserverStore)
        {
            // Validate parameters.
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            if (logObserverStore == null) throw new ArgumentNullException(nameof(logObserverStore));

            // Create the provider and add.
            loggerFactory.AddProvider(new ObservableLoggerProvider(logObserverStore));

            // Return the logger provider.
            return loggerFactory;
        }
    }
}
