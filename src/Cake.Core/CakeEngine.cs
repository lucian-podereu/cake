﻿using System;
using System.Diagnostics;
using System.Linq;
using Cake.Core.Diagnostics;
using Cake.Core.Graph;

namespace Cake.Core
{
    /// <summary>
    /// The Cake execution engine.
    /// </summary>
    public sealed class CakeEngine : AbstractCakeEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeEngine"/> class.
        /// </summary>
        /// <param name="log">The log.</param>
        public CakeEngine(ICakeLog log) : base(log)
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

            // This isn't pretty, but we need to keep track of exceptions thrown
            // while running a setup action, or a task. We do this since we don't
            // want to throw teardown exceptions if an exception was thrown previously.
            var exceptionWasThrown = false;

            try
            {
                PerformSetup(strategy);

                var stopWatch = new Stopwatch();
                var report = new CakeReport();

                foreach (var taskNode in graph.Traverse(target))
                {
                    bool isTarget;
                    var task = GetTask(target, taskNode, out isTarget);

                    Runner.ExecuteTask(context, strategy, stopWatch, task, isTarget, report);
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