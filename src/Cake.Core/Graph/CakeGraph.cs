using System;
using System.Collections.Generic;
using System.Linq;

namespace Cake.Core.Graph
{
    /// <summary>
    /// A graph containing the build actions and the dependencies between them.
    /// </summary>
    public sealed class CakeGraph
    {
        private readonly List<string> _nodes;
        private readonly List<CakeGraphEdge> _edges;

        /// <summary>
        /// Constructs a new <see cref="CakeGraph"/> object.
        /// </summary>
        public CakeGraph()
        {
            _nodes = new List<string>();
            _edges = new List<CakeGraphEdge>();
        }

        internal IReadOnlyList<string> Nodes
        {
            get { return _nodes; }
        }

        internal IReadOnlyList<CakeGraphEdge> Edges
        {
            get { return _edges; }
        }

        internal void Add(string node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }
            if (_nodes.Any(x => x == node))
            {
                throw new CakeException("Node has already been added to graph.");
            }
            _nodes.Add(node);
        }

        internal void Connect(string start, string end)
        {
            if (start.Equals(end, StringComparison.OrdinalIgnoreCase))
            {
                throw new CakeException("Reflexive edges in graph are not allowed.");
            }
            if (_edges.Any(x => x.Start.Equals(end, StringComparison.OrdinalIgnoreCase)
                && x.End.Equals(start, StringComparison.OrdinalIgnoreCase)))
            {
                throw new CakeException("Unidirectional edges in graph are not allowed.");
            }
            if (_edges.Any(x => x.Start.Equals(start, StringComparison.OrdinalIgnoreCase)
                && x.End.Equals(end, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            if (_nodes.All(x => !x.Equals(start, StringComparison.OrdinalIgnoreCase)))
            {
                _nodes.Add(start);
            }
            if (_nodes.All(x => !x.Equals(end, StringComparison.OrdinalIgnoreCase)))
            {
                _nodes.Add(end);
            }
            _edges.Add(new CakeGraphEdge(start, end));
        }

        internal bool Exist(string name)
        {
            return _nodes.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        internal IEnumerable<string> Traverse(string target)
        {
            if (!Exist(target))
            {
                return Enumerable.Empty<string>();
            }
            var result = new List<string>();
            Traverse(target, result);
            return result;
        }

        private void Traverse(string node, ICollection<string> result, ISet<string> visited = null)
        {
            visited = visited ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!visited.Contains(node))
            {
                visited.Add(node);
                var incoming = _edges.Where(x => x.End.Equals(node, StringComparison.OrdinalIgnoreCase)).Select(x => x.Start);
                foreach (var child in incoming)
                {
                    Traverse(child, result, visited);
                }
                result.Add(node);
            }
            else if (!result.Any(x => x.Equals(node, StringComparison.OrdinalIgnoreCase)))
            {
                throw new CakeException("Graph contains circular references.");
            }
        }

        /// <summary>
        /// Traverses the nodes and groups the nodes that can be executed in parallel.
        /// </summary>
        /// <param name="target"></param>
        /// <returns>
        /// The list of action nodes to be executed. 
        /// If an element in the result contains more nodes, they can be run in parallel.
        /// </returns>
        public IEnumerable<IEnumerable<string>> TraverseAndGroup(string target)
        {
            if (!Exist(target))
            {
                return new string[0][];
            }

            var result = new List<string>();
            Traverse(target, result);

            var mergedNodes = MergeParallelNodes(result).ToList();

            return mergedNodes;
        }

        private IEnumerable<IEnumerable<string>> MergeParallelNodes(IList<string> nodes)
        {
            var result = new List<string>();
            int i = 0;

            while (i < nodes.Count - 1)
            {
                if (DependsOn(nodes[i + 1], nodes[i]))
                {
                    if (result.Count > 0)
                    {
                        // add last parallel node and flush the buffer
                        result.Add(nodes[i]);
                        yield return new List<string>(result);

                        result.Clear();
                    }
                    else
                    {
                        // return non-parallel node
                        yield return new List<string> { nodes[i] };
                    }                   
                }
                else
                {
                    // buffer parallel nodes
                    result.Add(nodes[i]);
                }
                i++;
            }

            // return last node
            yield return new List<string> { nodes[i] };
        }

        private bool DependsOn(string node1, string node2)
        {
            var result = new List<string>();
            Traverse(node1, result);

            return result.Contains(node2);
        }
    }
}