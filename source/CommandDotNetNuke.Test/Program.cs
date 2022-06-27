using System;
using System.Reflection;
using CommandDotNet;
using CommandDotNet.Builders;
using CommandDotNetNuke.Converter;

namespace CommandDotNetNuke.Test;

public class DependencyResolver : IDependencyResolver
{
    public object Resolve(Type type)
    {
        if (type == typeof(NukeDescribeCommand))
        {
            return new NukeDescribeCommand(new ()
            {
                ShouldRemoveThisTask = false,
                ToolName = "Hello",
                Help = "Hello",
                Namespace = "Hello",
                OfficialURL = "url",
                OutputJsonPath = "test.json",
                OutputCSharpPath = "test.cs",
                PackageID = "id",
            });
        }

        return Activator.CreateInstance(type);
    }

    public bool TryResolve(Type type, out object item)
    {
        try
        {
            item = Resolve(type);
        }
        catch (Exception e)
        {
            item = null;
            if (e is MissingMethodException or TargetInvocationException)
                return false;
            throw;
        }
        return item != null;
    }
}

public class OtherCommands
{
    public class Arguments : IArgumentModel
    {
        [Option]
        public int a { get; set; }
        [Option]
        public string b { get; set; }

        public enum MyEnum
        {
            A, B, C,
        }
        [Option]
        public MyEnum e { get; set; }
    }

    [Command("thing", Description = "does stuff")]
    public static void Execute(Arguments arguments)
    {
        Console.WriteLine($"Was given a={arguments.a}, b={arguments.b}, e={arguments.e}");
    }
    
    [Subcommand]
    public NukeDescribeCommand NukeDescribe { get; set; }
}

public class Program
{
    public static int Main(string[] args)
    {
        var app = new AppRunner<NukeDescribeCommand>();
        app.UseDependencyResolver(new DependencyResolver());
        return app.Run(args);
    }
}
