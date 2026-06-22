using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Router
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine($"[LOG] {message}");
        }
    }

    public interface ISegmentMatcher
    {
        bool TryMatch(string segment, out object value);
    }

    public class StaticSegmentMatcher : ISegmentMatcher
    {
        private readonly string _expected;

        public StaticSegmentMatcher(string expected)
        {
            _expected = expected;
        }

        public bool TryMatch(string segment, out object value)
        {
            value = segment;
            return string.Equals(_expected, segment, StringComparison.Ordinal);
        }
    }

    public class IntSegmentMatcher : ISegmentMatcher
    {
        public bool TryMatch(string segment, out object value)
        {
            if (int.TryParse(segment, out int result))
            {
                value = result;
                return true;
            }
            value = null;
            return false;
        }
    }

    public class StringSegmentMatcher : ISegmentMatcher
    {
        public bool TryMatch(string segment, out object value)
        {
            value = segment;
            return true;
        }
    }

    public class FloatSegmentMatcher : ISegmentMatcher
    {
        public bool TryMatch(string segment, out object value)
        {
            if (float.TryParse(segment, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                value = result;
                return true;
            }
            value = null;
            return false;
        }
    }

    public class GuidSegmentMatcher : ISegmentMatcher
    {
        public bool TryMatch(string segment, out object value)
        {
            if (Guid.TryParse(segment, out Guid result))
            {
                value = result;
                return true;
            }
            value = null;
            return false;
        }
    }

    public class DateTimeSegmentMatcher : ISegmentMatcher
    {
        public bool TryMatch(string segment, out object value)
        {
            if (DateTime.TryParse(segment, out DateTime result))
            {
                value = result;
                return true;
            }
            value = null;
            return false;
        }
    }

    public class SegmentParser
    {
        private readonly Dictionary<string, Func<ISegmentMatcher>> _matchers = new();

        public SegmentParser()
        {
            RegisterType("int", () => new IntSegmentMatcher());
            RegisterType("string", () => new StringSegmentMatcher());
            RegisterType("float", () => new FloatSegmentMatcher());
            RegisterType("guid", () => new GuidSegmentMatcher());
            RegisterType("datetime", () => new DateTimeSegmentMatcher());
        }

        public void RegisterType(string typeName, Func<ISegmentMatcher> factory)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Type name cannot be empty");
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _matchers[typeName.ToLower()] = factory;
        }

        public (string paramName, ISegmentMatcher matcher) ParseDynamic(string segment)
        {
            string inner = segment.Substring(1, segment.Length - 2);
            string[] parts = inner.Split(':');

            if (parts.Length != 2)
                throw new ArgumentException($"Invalid dynamic segment format: '{segment}'. Expected {{name:type}}");

            string name = parts[0].Trim();
            string type = parts[1].Trim().ToLower();

            if (!_matchers.TryGetValue(type, out var factory))
                throw new ArgumentException($"Unknown segment type: '{type}'");

            return (name, factory());
        }

        public static bool IsDynamic(string segment)
        {
            return segment.StartsWith("{") && segment.EndsWith("}");
        }
    }

    class RouteTreeNode
    {
        public ISegmentMatcher Matcher { get; set; }
        public string ParamName { get; set; }
        public Delegate Handler { get; set; }
        public string[] ParamNames { get; set; }
        public List<RouteTreeNode> Children { get; set; } = new List<RouteTreeNode>();
    }

    class MatchResult
    {
        public RouteTreeNode Node { get; set; }
        public List<(string name, object value)> Values { get; set; }
    }

    public interface IRouter
    {
        void RegisterRoute(string template, Delegate handler);
        void Route(string route);
        Task RouteAsync(string route);
    }

    public class Router : IRouter
    {
        private readonly ILogger _logger;
        private readonly SegmentParser _parser;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private readonly Dictionary<string, Delegate> _staticRoutes = new();
        private readonly List<RouteTreeNode> _rootNodes = new();

        public Router(SegmentParser parser, ILogger logger = null)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger;
        }

        public void RegisterRoute(string template, Delegate handler)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Route template cannot be empty");
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _lock.EnterWriteLock();
            try
            {
                _logger?.Log($"Registering route: {template}");

                if (!template.Contains('{'))
                {
                    if (_staticRoutes.ContainsKey(template))
                        throw new InvalidOperationException($"Route '{template}' is already registered");
                    _staticRoutes[template] = handler;
                }
                else
                {
                    RegisterDynamic(template, handler);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void RegisterDynamic(string template, Delegate handler)
        {
            string[] segments = SplitRoute(template);
            string[] paramNames = handler.Method.GetParameters()
                                         .Select(p => p.Name)
                                         .ToArray();

            List<RouteTreeNode> currentLevel = _rootNodes;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                RouteTreeNode existing;

                if (SegmentParser.IsDynamic(seg))
                {
                    var (paramName, matcher) = _parser.ParseDynamic(seg);
                    existing = currentLevel.FirstOrDefault(n =>
                        n.Matcher is not StaticSegmentMatcher && n.ParamName == paramName);

                    if (existing == null)
                    {
                        existing = new RouteTreeNode { Matcher = matcher, ParamName = paramName };
                        currentLevel.Add(existing);
                    }
                }
                else
                {
                    existing = currentLevel.FirstOrDefault(n =>
                        n.Matcher is StaticSegmentMatcher && n.ParamName == seg);

                    if (existing == null)
                    {
                        existing = new RouteTreeNode
                        {
                            Matcher = new StaticSegmentMatcher(seg),
                            ParamName = seg
                        };
                        currentLevel.Add(existing);
                    }
                }

                if (i == segments.Length - 1)
                {
                    if (existing.Handler != null)
                        throw new InvalidOperationException($"Route '{template}' is already registered");
                    existing.Handler = handler;
                    existing.ParamNames = paramNames;
                }

                currentLevel = existing.Children;
            }
        }

        public void Route(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route cannot be empty");
            if (!route.StartsWith("/"))
                throw new ArgumentException("Route must start with '/'");

            _lock.EnterReadLock();
            try
            {
                _logger?.Log($"Routing request: {route}");

                if (_staticRoutes.TryGetValue(route, out var staticHandler))
                {
                    _logger?.Log($"Found static handler for: {route}");
                    InvokeHandler(staticHandler, Array.Empty<string>(), new List<(string, object)>());
                    return;
                }

                string[] segments = SplitRoute(route);
                var match = TryMatchTree(_rootNodes, segments, 0, new List<(string, object)>());

                if (match == null)
                {
                    _logger?.Log($"No handler found for: {route}");
                    throw new KeyNotFoundException($"No route matches '{route}'");
                }

                _logger?.Log("Invoking handler");
                InvokeHandler(match.Node.Handler, match.Node.ParamNames, match.Values);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public async Task RouteAsync(string route)
        {
            await Task.Run(() =>
            {
                try
                {
                    Route(route);
                }
                catch (KeyNotFoundException ex)
                {
                    _logger?.Log($"Route not found: {ex.Message}");
                    throw;
                }
                catch (ArgumentException ex)
                {
                    _logger?.Log($"Invalid route argument: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Unexpected error while routing '{route}': {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            });
        }

        private MatchResult TryMatchTree(List<RouteTreeNode> nodes, string[] segments, int depth,
            List<(string name, object value)> values)
        {
            if (depth == segments.Length)
                return null;

            string seg = segments[depth];

            foreach (var node in nodes)
            {
                if (!node.Matcher.TryMatch(seg, out object val))
                    continue;

                var newValues = new List<(string, object)>(values);
                if (node.Matcher is not StaticSegmentMatcher)
                    newValues.Add((node.ParamName, val));

                if (depth == segments.Length - 1)
                {
                    if (node.Handler != null)
                        return new MatchResult { Node = node, Values = newValues };
                }
                else
                {
                    var result = TryMatchTree(node.Children, segments, depth + 1, newValues);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        private static void InvokeHandler(Delegate handler, string[] paramNames,
            List<(string name, object value)> values)
        {
            var args = new object[paramNames.Length];
            for (int i = 0; i < paramNames.Length; i++)
            {
                string pName = paramNames[i];
                args[i] = values.FirstOrDefault(v => v.name == pName).value;
            }
            handler.DynamicInvoke(args);
        }

        private static string[] SplitRoute(string route)
        {
            return route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var logger = new ConsoleLogger();
            var parser = new SegmentParser();
            var router = new Router(parser, logger);

            router.RegisterRoute("/foo/bar/", () =>
            {
                Console.WriteLine("Static route: /foo/bar/");
            });

            router.RegisterRoute("/foo/bar/{p:int}/", (int p) =>
            {
                Console.WriteLine($"Dynamic route with int p={p}");
            });

            router.RegisterRoute("/foo/{name:string}/", (string name) =>
            {
                Console.WriteLine($"Dynamic route with string name={name}");
            });

            router.RegisterRoute("/foo/bar/{a:int}/{b:int}/", (int b, int a) =>
            {
                Console.WriteLine($"Two params: b={b}, a={a}");
            });

            Console.WriteLine("Registered routes:");
            Console.WriteLine("  /foo/bar/");
            Console.WriteLine("  /foo/bar/{p:int}/");
            Console.WriteLine("  /foo/{name:string}/");
            Console.WriteLine("  /foo/bar/{a:int}/{b:int}/");
            Console.WriteLine();
            Console.WriteLine("Enter the route to process (or 'exit' to exit):");

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                Console.WriteLine();

                if (input == null || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Completion of work.");
                    break;
                }

                string trimmed = input.Trim();

                if (trimmed.Length == 0)
                {
                    Console.WriteLine("Error: Empty input. Try again.");
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    await router.RouteAsync(trimmed);
                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine($"Error: Route not found. {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Error: Invalid route. {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
                }

                Console.WriteLine();
            }
        }
    }
}