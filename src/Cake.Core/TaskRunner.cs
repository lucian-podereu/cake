using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Cake.Core.Diagnostics;

namespace Cake.Core
{
    /// <summary>
    /// Used for running a build task/node, together with their setup and teardown actions
    /// </summary>
    public class TaskRunner
    {
        private readonly ICakeLog _log;

        /// <summary>
        /// Constructs a new <see cref="TaskRunner"/> object.
        /// </summary>
        /// <param name="log">The <see cref="ICakeLog"/> logger.</param>
        public TaskRunner(ICakeLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException("log");
            }

            _log = log;
        }

        /// <summary>
        /// The current setup action.
        /// </summary>
        public Action<ICakeContext, ITaskSetupContext> TaskSetupAction { get; set; }

        /// <summary>
        /// The current teardown action.
        /// </summary>
        public Action<ICakeContext, ITaskTeardownContext> TaskTeardownAction { get; set; }

        internal void ExecuteTask(ICakeContext context, IExecutionStrategy strategy, Stopwatch stopWatch, CakeTask task, bool isTarget, CakeReport report)
        {
            try
            {
                ExecuteTaskAsync(context, strategy, stopWatch, task, isTarget, report).Wait();
            }
            catch (AggregateException ae)
            {
                throw ae.InnerException;
            }
        }

        internal async Task ExecuteTaskAsync(ICakeContext context, IExecutionStrategy strategy, Stopwatch stopWatch, CakeTask task, bool isTarget, CakeReport report)
        {
            if (!ShouldTaskExecute(task, isTarget))
            {
                SkipTask(context, strategy, task);
                return;
            }

            // Reset the stop watch.
            stopWatch.Reset();
            stopWatch.Start();

            PerformTaskSetup(context, strategy, task, false);

            bool exceptionWasThrown = false;
            try
            {
                // Execute the task.
                await strategy.ExecuteAsync(task, context);
            }
            catch (Exception exception)
            {
                _log.Error("An error occured when executing task '{0}'.", task.Name);

                exceptionWasThrown = true;

                // Got an error reporter?
                if (task.ErrorReporter != null)
                {
                    ReportErrors(strategy, task.ErrorReporter, exception);
                }

                // Got an error handler?
                if (task.ErrorHandler != null)
                {
                    HandleErrors(strategy, task.ErrorHandler, exception);
                }
                else
                {
                    // No error handler defined for this task.
                    // Rethrow the exception and let it propagate.
                    throw;
                }
            }
            finally
            {
                if (task.FinallyHandler != null)
                {
                    strategy.InvokeFinally(task.FinallyHandler);
                }

                PerformTaskTeardown(context, strategy, task, stopWatch.Elapsed, false, exceptionWasThrown);
            }

            // Add the task results to the report.
            report.Add(task.Name, stopWatch.Elapsed);
        }

        private static bool ShouldTaskExecute(CakeTask task, bool isTarget)
        {
            foreach (var criteria in task.Criterias)
            {
                if (!criteria())
                {
                    if (!isTarget)
                        return false;

                    // It's not OK to skip the target task.
                    // See issue #106 (https://github.com/cake-build/cake/issues/106)
                    const string format = "Could not reach target '{0}' since it was skipped due to a criteria.";
                    var message = string.Format(CultureInfo.InvariantCulture, format, task.Name);
                    throw new CakeException(message);
                }
            }

            return true;
        }

        internal void SkipTask(ICakeContext context, IExecutionStrategy strategy, CakeTask task)
        {
            PerformTaskSetup(context, strategy, task, true);
            strategy.Skip(task);
            PerformTaskTeardown(context, strategy, task, TimeSpan.Zero, true, false);
        }

        private void PerformTaskSetup(ICakeContext context, IExecutionStrategy strategy, ICakeTaskInfo task, bool skipped)
        {
            // Trying to stay consistent with the behavior of script-level Setup & Teardown (if setup fails, don't run the task, but still run the teardown)
            if (TaskSetupAction == null)
                return;

            try
            {
                var taskSetupContext = new TaskSetupContext(task);
                strategy.PerformTaskSetup(TaskSetupAction, context, taskSetupContext);
            }
            catch
            {
                PerformTaskTeardown(context, strategy, task, TimeSpan.Zero, skipped, true);
                throw;
            }
        }

        private void PerformTaskTeardown(ICakeContext context, IExecutionStrategy strategy, ICakeTaskInfo task, TimeSpan duration, bool skipped, bool exceptionWasThrown)
        {
            if (TaskTeardownAction == null)
                return;

            var taskTeardownContext = new TaskTeardownContext(task, duration, skipped);
            try
            {
                strategy.PerformTaskTeardown(TaskTeardownAction, context, taskTeardownContext);
            }
            catch (Exception ex)
            {
                _log.Error("An error occured in the custom task teardown action ({0}).", task.Name);
                if (!exceptionWasThrown)
                {
                    // If no other exception was thrown, we throw this one.
                    throw;
                }
                _log.Error("Task Teardown error ({0}): {1}", task.Name, ex.ToString());
            }
        }


        private static void ReportErrors(IExecutionStrategy strategy, Action<Exception> errorReporter, Exception taskException)
        {
            try
            {
                strategy.ReportErrors(errorReporter, taskException);
            }
                // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
                // Ignore errors from the error reporter.
            }
        }

        private void HandleErrors(IExecutionStrategy strategy, Action<Exception> errorHandler, Exception exception)
        {
            try
            {
                strategy.HandleErrors(errorHandler, exception);
            }
            catch (Exception errorHandlerException)
            {
                if (errorHandlerException != exception)
                {
                    _log.Error("Error: {0}", exception.Message);
                }
                throw;
            }
        }
    }
}