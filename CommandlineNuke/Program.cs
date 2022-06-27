using CommandLine;
using CommandLine.Core;
using Nuke.CodeGeneration.Model;

namespace CommandLineNuke;

public class TestClass
{
    [Option]
    public string Hello { get; set; }
}

public class TestClass2
{
    [Option]
    public string Hello { get; set; }
}

public class Program
{
    private static readonly System.Type[] _Commands = new[] { typeof(TestClass), typeof(TestClass2) };
    public static void Main(string[] args)
    {
        Tool tool = new();
        tool.Help = "Stuff";
        tool.Name = "Name";
        tool.OfficialUrl = "url";
        tool.CustomExecutable = true;
        
        var tasks = tool.Tasks;
        foreach (var command in _Commands)
        {
            var verb = command.GetVerbSpecification();
            var task = new Task();
            if (verb.MatchJust(out var verbAttribute))
            {
                task.Help = verbAttribute.HelpText;
                task.Postfix = verbAttribute.Name;
            }
            else
            {
                task.Postfix = command.Name;
            }

            foreach (var (prop, attr) in command.GetSpecifications())
            {
                if (attr is OptionAttribute optionAttribute)
                {
                    
                }
                else if (attr is ValueAttribute valueAttribute)
                {
                    
                }
            }
        }
    }
}