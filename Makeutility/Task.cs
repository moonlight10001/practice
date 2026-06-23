using System.Collections.Generic;

namespace MakeUtility
{
    class Task
    {
        public string Name { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> Actions { get; set; } = new List<string>();
    }
}
