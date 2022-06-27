using System;
using System.Reflection;
using CommandDotNet;
using CommandDotNet.Builders;

#if COMPILE_WITH_NUKE_DESCRIBE
using CommandDotNetNuke.Converter;
#endif

namespace CommandDotNetNuke.Test;

public class DependencyResolver : IDependencyResolver
{
    public object Resolve(Type type)
    {
        #if COMPILE_WITH_NUKE_DESCRIBE
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
                    // PackageID = "id",
                    PackageExecutablePath = "hello",
                    PackageExecutableName = "hello",
                });
            }
        #endif

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
    public void Execute(Arguments arguments)
    {
        Console.WriteLine($"Was given a={arguments.a}, b={arguments.b}, e={arguments.e}");
    }

    #if COMPILE_WITH_NUKE_DESCRIBE
        [Subcommand]
        public NukeDescribeCommand NukeDescribe { get; set; }
    #endif
}

public class Program
{
    public static int Main(string[] args)
    {
        var app = new AppRunner<OtherCommands>();
        app.UseDependencyResolver(new DependencyResolver());
        return app.Run(args);
    }
}
