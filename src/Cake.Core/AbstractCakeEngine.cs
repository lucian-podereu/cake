using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Cake.Core.Diagnostics;
using Cake.Core.Graph;

namespace Cake.Core
{
    /// <summary>
    /// Base class for creating Cake engines.
    /// </summary>
    public abstract class AbstractCakeEngine : ICakeEngine
    {
        private readonly List<CakeTask> _tasks;

        /// <summary>
        /// Runs the specified target using the specified <see cref="IExecutionStrategy"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="strategy">The execution strategy.</param>
        /// <param name="target">The target to run.</param>
        /// <returns>The resulting report.</returns>
        public abstract CakeReport RunTarget(ICakeContext context, IExecutionStrategy strategy, string target);

        /// <summary>
        /// Registers a new task.
        /// </summary>
        /// <param name="name">The name of the task.</param>
        /// <returns>A <see cref="CakeTaskBuilder{ActionTask}"/>.</returns>
        public virtual CakeTaskBuilder<ActionTask> RegisterTask(string name)
        {
            if (Tasks.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                const string format = "Another task with the name '{0}' has already been added.";
                throw new CakeException(string.Format(CultureInfo.InvariantCulture, format, name));
            }

            var task = new ActionTask(name);
            _tasks.Add(task);

            return new CakeTaskBuilder<ActionTask>(task);
        }
        /// <summary>
        /// Allows registration of an action that's executed before any tasks are run.
        /// If setup fails, no tasks will be executed but teardown will be performed.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        public virtual void RegisterSetupAction(Action action)
        {
            SetupAction = action;
        }

        /// <summary>
        /// Allows registration of an action that's executed after all other tasks have been run.
        /// If a setup action or a task fails with or without recovery, the specified teardown action will still be executed.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        public virtual void RegisterTeardownAction(Action action)
        {
            TeardownAction = action;
        }
      
        /// <summary>
        /// Allows registration of an action that's executed before each task is run.
        /// If the task setup fails, the task will not be executed but the task's teardown will be performed.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        public virtual void RegisterTaskSetupAction(Action<ICakeContext, ITaskSetupContext> action)
        {
            Runner.TaskSetupAction = action;
        }

        /// <summary>
        /// Allows registration of an action that's executed after each task has been run.
        /// If a task setup action or a task fails with or without recovery, the specified task teardown action will still be executed.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        public virtual void RegisterTaskTeardownAction(Action<ICakeContext, ITaskTeardownContext> action)
        {
            Runner.TaskTeardownAction = action;
        }

        /// <summary>
        /// The logger.
        /// </summary>
        protected readonly ICakeLog Log;


        /// <summary>
        /// The build setup action.
        /// </summary>
        protected Action SetupAction;

        /// <summary>
        /// The build teardown action.
        /// </summary>
        protected Action TeardownAction;

        /// <summary>
        /// The <see cref="TaskRunner"/> instance used to run each task.
        /// </summary>
        protected readonly TaskRunner Runner;

        /// <summary>
        /// Base ctor.
        /// </summary>
        /// <param name="log">The <see cref="ICakeLog"/> logger.</param>
        protected AbstractCakeEngine(ICakeLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException("log");
            }

            Log = log;
            _tasks = new List<CakeTask>();
            Runner = new TaskRunner(log);
        }

        /// <summary>
        /// Gets all registered tasks.
        /// </summary>
        /// <value>The registered tasks.</value>
        public IReadOnlyList<CakeTask> Tasks
        {
            get { return _tasks; }
        }

        /// <summary>
        /// Gets the <see cref="CakeTask"/> for a specific node.
        /// </summary>
        /// <param name="target">The target (final) action node.</param>
        /// <param name="taskNode">The task node to be retrieved.</param>
        /// <param name="isTarget">Is set to true if this is also the target (final) node; otherwise is false.</param>
        /// <returns></returns>
        protected virtual CakeTask GetTask(string target, string taskNode, out bool isTarget)
        {
            var task = _tasks.FirstOrDefault(x => x.Name.Equals(taskNode, StringComparison.OrdinalIgnoreCase));
            Debug.Assert(task != null, "Node should not be null.");

            // Is this the current target?
            isTarget = task.Name.Equals(target, StringComparison.OrdinalIgnoreCase);
            return task;
        }

        /// <summary>
        /// Performs the build setup action.
        /// </summary>
        /// <param name="strategy">The <see cref="IExecutionStrategy"/> use to execute the setup.</param>
        protected virtual void PerformSetup(IExecutionStrategy strategy)
        {
            if (SetupAction == null)
                return;

            strategy.PerformSetup(SetupAction);
        }

        /// <summary>
        /// Performs the build teardown.
        /// </summary>
        /// <param name="strategy">The <see cref="IExecutionStrategy"/> use to execute the teardown.</param>
        /// <param name="exceptionWasThrown">True if an exception was thrown on execution; otherwise false.</param>
        protected virtual void PerformTeardown(IExecutionStrategy strategy, bool exceptionWasThrown)
        {
            if (TeardownAction == null)
                return;

            try
            {
                strategy.PerformTeardown(TeardownAction);
            }
            catch (Exception ex)
            {
                Log.Error("An error occured in the custom teardown action.");
                if (!exceptionWasThrown)
                {
                    // If no other exception was thrown, we throw this one.
                    throw;
                }
                Log.Error("Teardown error: {0}", ex.ToString());
            }
        }

        /// <summary>
        /// Throws an <see cref="CakeException"/> if the specified node doesn't exist.
        /// </summary>
        /// <param name="target">The target node.</param>
        /// <param name="graph">The node graph.</param>
        /// <exception cref="CakeException">An exception of this type is thrown if the node isn't found.</exception>
        protected static void ThrowIfTargetNotFound(string target, CakeGraph graph)
        {
            if (graph.Exist(target))
                return;

            const string format = "The target '{0}' was not found.";
            throw new CakeException(string.Format(CultureInfo.InvariantCulture, format, target));
        }
    }
}
