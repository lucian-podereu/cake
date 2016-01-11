using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Cake.Core.Diagnostics;
using Cake.Core.Graph;

namespace Cake.Core
{
    /// <summary>
    /// The Cake execution engine, with support for parallel execution of tasks.
    /// </summary>
    public sealed class ParallelCakeEngine : AbstractCakeEngine
    {
        /// <summary>
        /// Constructs a new <see cref="ParallelCakeEngine"/> object.
        /// </summary>
        /// <param name="log">The <see cref="ICakeLog"/> logger.</param>
        public ParallelCakeEngine(ICakeLog log) : base(log)
        {
        }

        /// <summary>
        /// Runs the specified target.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="strategy">The execution strategy.</param>
        /// <param name="target">The target to run.</param>
        /// <returns>The resulting report.</returns>
        public override CakeReport RunTarget(ICakeContext context, IExecutionStrategy strategy, string target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }
            if (strategy == null)
            {
                throw new ArgumentNullException("strategy");
            }

            var graph = CakeGraphBuilder.Build(Tasks.ToList());

            ThrowIfTargetNotFound(target, graph);

            var exceptionWasThrown = false;

            try
            {
                PerformSetup(strategy);

                var stopWatch = new Stopwatch();
                var report = new CakeReport();

                foreach (IEnumerable<string> parallelNodes in graph.TraverseAndGroup(target))
                {
                    var runningTasks = new List<Task>();

                    foreach (var taskNode in parallelNodes)
                    {
                        bool isTarget;
                        var task = GetTask(target, taskNode, out isTarget);

                        var newTask = Runner.ExecuteTaskAsync(context, strategy, stopWatch, task, isTarget, report);
                        runningTasks.Add(newTask);
                    }

                    Task.WaitAll(runningTasks.ToArray());
                }

                return report;
            }
            catch
            {
                exceptionWasThrown = true;
                throw;
            }
            finally
            {
                PerformTeardown(strategy, exceptionWasThrown);
            }
        }
    }
}
