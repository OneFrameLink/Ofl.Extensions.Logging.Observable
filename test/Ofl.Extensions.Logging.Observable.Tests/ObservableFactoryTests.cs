using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Linq;
using Ofl.Threading.Tasks;

namespace Ofl.Extensions.Logging.Observable.Tests
{
    public class ObservableFactoryTests : IClassFixture<LogObserverStoreClassFixture>
    {
        #region Constructor.

        public ObservableFactoryTests(LogObserverStoreClassFixture logObserverStoreClassFixture)
        {
            // Validate parameters.
            _logObserverStoreClassFixture = logObserverStoreClassFixture ?? throw new ArgumentNullException(nameof(logObserverStoreClassFixture));
        }

        #endregion

        #region Instance, read-only state.

        private readonly LogObserverStoreClassFixture _logObserverStoreClassFixture;

        #endregion

        #region Helpers

        private (ILoggerFactory Logger, ILogObservableFactory Observable) SetupEnvironment()
        {
            // The observer store.
            ILogObserverStore logObserverStore = _logObserverStoreClassFixture.LogObserverStore;

            // Create teh logger factory.
            ILoggerFactory loggerFactory = new LoggerFactory().AddDebug()
                .AddObservable(logObserverStore);

            // Create the observer.
            ILogObservableFactory logObservableFactory = new LogObservableFactory(logObserverStore);

            // Return the tuple.
            return (loggerFactory, logObservableFactory);
        }

        #endregion

        #region Tests.

        [Fact]
        public async Task Test_LoggerFlowsToNewThreadTasks()
        {
            // Setup the environment.
            (ILoggerFactory Logger, ILogObservableFactory Observable) env = SetupEnvironment();

            // Create the logger
            ILogger logger = env.Logger.CreateLogger($"{ typeof(ObservableFactoryTests).FullName }.{ nameof(Test_LoggerFlowsToNewThreadTasks) }");

            // Create the observable, do nothing here.
            IObservable<LogEntry> observable = env.Observable.Create();

            // The list of all events.
            var allLogEntries = new List<LogEntry>();

            // The list of main thread events.
            var mainThreadEvents = new List<LogEntry>();

            // Create two task completion sources.
            var tcs1 = new TaskCompletionSource<object>();
            var tcs2 = new TaskCompletionSource<object>();

            // Observe now.
            using (observable.Subscribe(le => allLogEntries.Add(le)))
            {
                // Run on threadpool threads.
                ThreadPool.QueueUserWorkItem(o => { logger.LogInformation("From thread 01."); tcs1.SetResult(null); });
                ThreadPool.QueueUserWorkItem(o => { logger.LogInformation("From thread 02."); tcs2.SetResult(null); });

                // Await, don't want pool results affecting main thread.
                await Task.WhenAll(tcs1.Task, tcs2.Task).ConfigureAwait(false);

                // The main thread logger, subscribe here.
                // Can subscribe on the same observable.
                using (observable.Subscribe(le => mainThreadEvents.Add(le)))
                    // Log here.
                    logger.LogInformation("From main thread.");
            }

            // Log one more time.
            logger.LogInformation("From main thread again, should not get logged.");

            // Validate.
            Assert.Equal(3, allLogEntries.Count);

            // Assert contents.
            Assert.NotNull(allLogEntries.Cast<LogEntry?>().SingleOrDefault(le => le.Value.FormattedMessage == "From thread 01."));
            Assert.NotNull(allLogEntries.Cast<LogEntry?>().SingleOrDefault(le => le.Value.FormattedMessage == "From thread 02."));
            Assert.NotNull(allLogEntries.Cast<LogEntry?>().SingleOrDefault(le => le.Value.FormattedMessage == "From main thread."));

            // Check main thread entries only.
            Assert.Single(mainThreadEvents);

            // Assert contents.
            Assert.NotNull(mainThreadEvents.Select(o => (LogEntry?) o).SingleOrDefault(le => le.Value.FormattedMessage == "From main thread."));
        }

        [Fact]
        public async Task Test_ChildrenDoNotImpactEachOther()
        {
            // Setup the environment.
            (ILoggerFactory Logger, ILogObservableFactory Observable) env = SetupEnvironment();

            // Create the logger
            ILogger logger = env.Logger.CreateLogger($"{ typeof(ObservableFactoryTests).FullName }.{ nameof(Test_ChildrenDoNotImpactEachOther) }");

            // Create the observable, do nothing here.
            IObservable<LogEntry> observable = env.Observable.Create();

            // Create two task completion sources.
            var subscribeSignal1 = new TaskCompletionSource<object>();
            var subscribeSignal2 = new TaskCompletionSource<object>();
            var loggedSignal1 = new TaskCompletionSource<object>();
            var loggedSignal2 = new TaskCompletionSource<object>();
            var firstCheckSignal1 = new TaskCompletionSource<object>();
            var firstCheckSignal2 = new TaskCompletionSource<object>();

            // The messages that were written.
            var threadLogEntries = new ConcurrentBag<LogEntry>();

            // Helper function.
            async Task HelperAsync(TaskCompletionSource<object> subscribeSignal, Task subscribeWait,
                TaskCompletionSource<object> logSignal, Task logWait,
                TaskCompletionSource<object> finishSignal)
            {
                // The items.
                var collected = new List<LogEntry>(1);

                // Subscribe and create a logger.
                using (observable.Subscribe(le => { collected.Add(le); threadLogEntries.Add(le); }))
                {
                    // Signal that this is subscribed.
                    subscribeSignal.SetResult(null);

                    // Wait on subscription.
                    await subscribeWait.ConfigureAwait(false);

                    // Message.
                    string message = $"From threadpool thread: {Guid.NewGuid():N}";

                    // Log.
                    logger.LogInformation(message);

                    // Signal.
                    logSignal.SetResult(null);

                    // Wait.
                    await logWait.ConfigureAwait(false);

                    // Validate message is ours.
                    Assert.Equal(message, collected.Single().FormattedMessage);

                    // Let finish signal go.
                    finishSignal.SetResult(null);
                }
            }

            // The count of messages.  We can't know wha
            var all = new ConcurrentBag<LogEntry>();

            // Subscribe here, just to make sure there's an added observer to the mix.
            using (observable.Subscribe(le => all.Add(le)))
            {
                // Log a message.
                logger.LogInformation("Logged from the main thread.");

                // Run on threadpool threads.
                ThreadPool.QueueUserWorkItem(o => HelperAsync(subscribeSignal1, subscribeSignal2.Task,
                    loggedSignal1, loggedSignal2.Task, firstCheckSignal1).RunInContext());
                ThreadPool.QueueUserWorkItem(o => HelperAsync(subscribeSignal2, subscribeSignal1.Task,
                    loggedSignal2, loggedSignal1.Task, firstCheckSignal2).RunInContext());

                // Wait on signals.
                await Task.WhenAll(firstCheckSignal1.Task, firstCheckSignal2.Task).ConfigureAwait(false);
            }

            // Assert total count.
            Assert.Equal(3, all.Count);

            // Assert messages.
            Assert.NotNull(all.Cast<LogEntry?>().Single(le => le.Value.FormattedMessage == "Logged from the main thread."));
            Assert.NotNull(all.Cast<LogEntry?>().Single(le => le.Value.FormattedMessage == threadLogEntries.First().FormattedMessage));
            Assert.NotNull(all.Cast<LogEntry?>().Single(le => le.Value.FormattedMessage == threadLogEntries.ElementAt(1).FormattedMessage));
        }

        [Fact]
        public async Task Test_ParentDoesNotImpactChild()
        {
            // Setup the environment.
            (ILoggerFactory Logger, ILogObservableFactory Observable) env = SetupEnvironment();

            // Create the logger
            ILogger logger = env.Logger.CreateLogger($"{ typeof(ObservableFactoryTests).FullName }.{ nameof(Test_ParentDoesNotImpactChild) }");

            // Create the observable, do nothing here.
            IObservable<LogEntry> observable = env.Observable.Create();

            // The signals for the children as well as the parent log signal.
            var subscribeSignal1 = new TaskCompletionSource<object>();
            var subscribeSignal2 = new TaskCompletionSource<object>();
            var logSignal = new TaskCompletionSource<object>();
            var threadDoneSignal1 = new TaskCompletionSource<object>();
            var threadDoneSignal2 = new TaskCompletionSource<object>();

            // Helper function.
            async Task HelperAsync(TaskCompletionSource<object> subscribeSignal, Task logWait,
                TaskCompletionSource<object> threadDoneSignal)
            {
                // The items.
                var collected = new List<LogEntry>();

                // Subscribe and create a logger.
                using (observable.Subscribe(le => collected.Add(le)))
                {
                    // Signal that this is subscribed.
                    subscribeSignal.SetResult(null);

                    // Wait on the logging.
                    await logWait.ConfigureAwait(false);

                    // Logging was performed from the main thread.
                    // Validate that nothing was collected.
                    Assert.Empty(collected);
                }

                // Signal the thread is done.
                threadDoneSignal.SetResult(null);
            }


            // Run on threadpool threads.
            ThreadPool.QueueUserWorkItem(o => HelperAsync(subscribeSignal1, logSignal.Task, threadDoneSignal1).RunInContext());
            ThreadPool.QueueUserWorkItem(o => HelperAsync(subscribeSignal2, logSignal.Task, threadDoneSignal2).RunInContext());

            // Wait on subscriptions.
            await Task.WhenAll(subscribeSignal1.Task, subscribeSignal2.Task).ConfigureAwait(false);

            // Log.
            logger.LogInformation("This will not show up in children.");

            // Signal the log wait is done.
            logSignal.SetResult(null);

            // Wait on the threads being done.
            await Task.WhenAll(threadDoneSignal1.Task, threadDoneSignal2.Task).ConfigureAwait(false);
        }

        [Fact]
        public void Test_NeverCompletes()
        {
            // Setup the environment.
            (ILoggerFactory Logger, ILogObservableFactory Observable) env = SetupEnvironment();

            // Create the logger
            ILogger logger = env.Logger.CreateLogger($"{ typeof(ObservableFactoryTests).FullName }.{ nameof(Test_NeverCompletes) }");

            // Create the observable, do nothing here.
            IObservable<LogEntry> observable = env.Observable.Create();

            // Flags.
            int onNextCount = 0, onCompleteCount = 0;

            // Subscribe and create a logger.
            using (observable.Subscribe(le => onNextCount++, () => onCompleteCount++))
                // Log.
                logger.LogInformation("Test message");

            // Assert.
            Assert.Equal(1, onNextCount);
            Assert.Equal(0, onCompleteCount);
        }

        #endregion
    }
}
