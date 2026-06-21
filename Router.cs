using System;
using System.Collections.Generic;
using System.Linq;
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

    static class SegmentParser
    {
        public static (string paramName, ISegmentMatcher matcher) ParseDynamic(string segment)
        {
            string inner = segment.Substring(1, segment.Length - 2);
            string[] parts = inner.Split(':');

            if (parts.Length != 2)
                throw new ArgumentException($"Invalid dynamic segment format: '{segment}'. Expected {{name:type}}");

            string name = parts[0].Trim();
            string type = parts[1].Trim().ToLower();

            ISegmentMatcher matcher = type switch
            {
                "int" => new IntSegmentMatcher(),
                "string" => new StringSegmentMatcher(),
                "float" => new FloatSegmentMatcher(),
                "guid" => new GuidSegmentMatcher(),
                "datetime" => new DateTimeSegmentMatcher(),
                _ => throw new ArgumentException($"Unknown segment type: '{type}'")
            };

            return (name, matcher);
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

    public interface IRouter
    {
        void RegisterRoute(string template, Action action);
        void RegisterRoute<T1>(string template, Action<T1> action);
        void RegisterRoute<T1, T2>(string template, Action<T1, T2> action);
        void Route(string route);
        Task RouteAsync(string route);
    }

    public class Router : IRouter
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, Action> _staticRoutes = new();
        private readonly List<RouteTreeNode> _rootNodes = new();

        public Router(ILogger logger = null)
        {
            _logger = logger;
        }

        public void RegisterRoute(string template, Action action)
        {
            ValidateTemplate(template);

            _logger?.Log($"Registering static route: {template}");

            if (_staticRoutes.ContainsKey(template))
                throw new InvalidOperationException($"Route '{template}' is already registered");

            _staticRoutes[template] = action;
        }

        public void RegisterRoute<T1>(string template, Action<T1> action)
        {
            ValidateTemplate(template);

            _logger?.Log($"Registering route: {template}");
            RegisterDynamic(template, action);
        }

        public void RegisterRoute<T1, T2>(string template, Action<T1, T2> action)
        {
            ValidateTemplate(template);

            _logger?.Log($"Registering route: {template}");
            RegisterDynamic(template, action);
        }

        private static void ValidateTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Route template cannot be empty");
        }

        private void RegisterDynamic(string template, Delegate handler)
        {
            string[] segments = SplitRoute(template);
            var paramNames = handler.Method.GetParameters().Select(p => p.Name).ToArray();

            List<RouteTreeNode> currentLevel = _rootNodes;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                RouteTreeNode existing;

                if (SegmentParser.IsDynamic(seg))
                {
                    var (paramName, matcher) = SegmentParser.ParseDynamic(seg);
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

            _logger?.Log($"Routing request: {route}");

            if (_staticRoutes.TryGetValue(route, out var staticHandler))
            {
                _logger?.Log($"Found static handler for: {route}");
                staticHandler();
                return;
            }

            string[] segments = SplitRoute(route);
            var collectedValues = new List<(string name, object value)>();
            bool found = TryMatchTree(_rootNodes, segments, 0, collectedValues);

            if (!found)
            {
                _logger?.Log($"No handler found for: {route}");
                throw new KeyNotFoundException($"No route matches '{route}'");
            }
        }

        public async Task RouteAsync(string route)
        {
            await Task.Run(() => Route(route));
        }

        private bool TryMatchTree(List<RouteTreeNode> nodes, string[] segments, int depth,
            List<(string name, object value)> values)
        {
            if (depth == segments.Length)
                return false;

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
                    {
                        InvokeHandler(node, newValues);
                        return true;
                    }
                }
                else
                {
                    if (TryMatchTree(node.Children, segments, depth + 1, newValues))
                        return true;
                }
            }

            return false;
        }

        private void InvokeHandler(RouteTreeNode node, List<(string name, object value)> values)
        {
            _logger?.Log("Invoking handler");

            var args = new object[node.ParamNames.Length];
            for (int i = 0; i < node.ParamNames.Length; i++)
            {
                string pName = node.ParamNames[i];
                args[i] = values.FirstOrDefault(v => v.name == pName).value;
            }

            node.Handler.DynamicInvoke(args);
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
            var router = new Router(logger);

            router.RegisterRoute("/foo/bar/", () =>
            {
                Console.WriteLine("Static route: /foo/bar/");
            });

            router.RegisterRoute<int>("/foo/bar/{p:int}/", (p) =>
            {
                Console.WriteLine($"Dynamic route with int p={p}");
            });

            router.RegisterRoute<string>("/foo/{name:string}/", (name) =>
            {
                Console.WriteLine($"Dynamic route with string name={name}");
            });

            router.RegisterRoute<int, int>("/foo/bar/{a:int}/{b:int}/", (b, a) =>
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
                    Console.WriteLine("Error: Empty input. Please try again.");
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    router.Route(trimmed);
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