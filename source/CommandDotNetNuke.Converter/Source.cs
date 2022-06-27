using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CommandDotNet;
using Microsoft.CodeAnalysis.CSharp;
using Nuke.CodeGeneration;
using Nuke.CodeGeneration.Model;

namespace CommandDotNetNuke.Converter;
using NukeTool = Tool;
using NukeTask = Task;

public sealed class NukeDescribeCommand
{
    public sealed class Configuration
    {
        public bool ShouldRemoveThisTask = true;
        public string ToolName;
        public string Help;
        public string Namespace;
        public string OfficialURL;
        public string OutputJsonPath;
        public string OutputCSharpPath;
        public string PackageID;
    }

    private readonly Configuration _configuration;

    public NukeDescribeCommand(Configuration configuration)
    {
        _configuration = configuration;
    }

    [Command("nuke-describe", Description = "Writes nuke json and generated code for this app.")]
    public void Execute(CommandContext commandContext)
    {
        var tool = ConvertionHelper.ConvertToTool(commandContext);
        
        if (_configuration.ShouldRemoveThisTask)
        {
            int indexOfThis = tool.Tasks.FindIndex(t => t.Postfix == "NukeDescribe");
            Debug.Assert(indexOfThis != -1, "indexOfThis != -1");
            tool.Tasks.RemoveAt(indexOfThis);
        }
        
        tool.Name = _configuration.ToolName;
        tool.OfficialUrl = _configuration.OfficialURL;
        tool.PackageId = _configuration.PackageID;
        tool.CustomExecutable = true;
        tool.Help = _configuration.Help;
        tool.Namespace = _configuration.Namespace;
        
        if (_configuration.OutputJsonPath is not null)
            ToolSerializer.Save(tool, _configuration.OutputJsonPath);
        if (_configuration.OutputCSharpPath is not null)
            CodeGenerator.GenerateCode(tool, Path.GetFullPath(_configuration.OutputCSharpPath));
    }
}

public static class ConvertionHelper
{
    public static void AppendEnumerationsToTool(NukeTool tool, HashSet<Type> enumerations)
    {
        var enums = tool.Enumerations;
        foreach (var enumType in enumerations)
        {
            var enumeration = new Enumeration();
            enumeration.Name = enumType.Name;
            enumeration.Tool = tool;
            enumeration.Values = enumType.GetEnumNames().ToList();
            
            enums.Add(enumeration);
        }
    }

    public static NukeTool ConvertToTool(CommandContext commandContext)
    {
        var rootCommand = commandContext.RootCommand;
        var tool = new NukeTool();
        var context = new NukeTaskConversionContext(
            tool.Tasks, new(), tool);
        if (rootCommand is not null)
        {
            AddNukeTasksRecursively(rootCommand, commandTypeNamePrefix: "", path: "", context);
            AppendEnumerationsToTool(tool, context.Enumerations);
        }
        return tool;
    }

    public record struct NukeTaskConversionContext(
        List<Task> Tasks,
        HashSet<Type> Enumerations,
        // List<DataClass> DataClasses,
        // Dictionary<string, List<Property>> CommonPropertySets,
        NukeTool Tool)
    {
    }

    private static readonly StringBuilder _StringBuilderCached = new();
    public static string ToPascalCase(ReadOnlySpan<char> input)
    {
        int i = 0;
        if (input.Length == 0)
            return "";
        
        {
            char ch = input[i];
            i++;

            if (ch >= 'a' && ch <= 'z')
                _StringBuilderCached.Append((char)(ch + 'A' - 'a'));
            else
                _StringBuilderCached.Append(ch);
        }
        
        while (i != input.Length)
        {
            char ch = input[i];
            i++;
            
            if (ch == ' ' || ch == '_' || ch == '-')
            {
                if (i == input.Length)
                    break;
                ch = input[i];
                if (ch >= 'a' && ch <= 'z')
                {
                    _StringBuilderCached.Append((char)(ch + 'A' - 'a'));
                    i++;
                }
            }
            else
            {
                _StringBuilderCached.Append(ch);
            }
        }

        {
            var result = _StringBuilderCached.ToString();
            _StringBuilderCached.Clear();
            return result;
        }
    }
    
    public static void AddNukeTasksRecursively(
        Command command,
        string commandTypeNamePrefix,
        string path,
        in NukeTaskConversionContext context)
    {
        if (command.IsExecutable)
        {
            commandTypeNamePrefix += ToPascalCase(command.Name);

            if (!command.IsRootCommand())
            {
                if (path != "")
                    path += " ";
                path += command.Name;
            }
            
            {
                var task = new NukeTask();
                context.Tasks.Add(task);
                task.Help = command.Description;
                task.Postfix = commandTypeNamePrefix;
                task.Tool = context.Tool;
                task.DefiniteArgument = path;
                
                var settingsClass = task.SettingsClass;
                settingsClass.Name = commandTypeNamePrefix;
                settingsClass.Tool = context.Tool;
                settingsClass.Task = task;
                // context.DataClasses.Add(settingsClass);
                
                var props = settingsClass.Properties;
                foreach (var operand in command.Operands)
                {
                    Property prop = new();
                    prop.Format = "{value}";
                    AddInfo(operand, prop, context);
                }
    
                foreach (var option in command.Options)
                {
                    Property prop = new();
                    string name = option.Name;
                    if (option.BooleanMode == BooleanMode.Implicit)
                        prop.Format = "--" + name;
                    else
                        prop.Format = "--" + name + "={value}";
                    AddInfo(option, prop, context);
                }
    
                void AddInfo(IArgument arg, Property prop, in NukeTaskConversionContext context)
                {
                    var typeInfo = arg.TypeInfo;
                    var underlyingType = typeInfo.UnderlyingType;
                    if (underlyingType.IsEnum)
                        context.Enumerations.Add(underlyingType);
                    prop.DataClass = settingsClass;
                    FillInCommonArgumentThings(arg, prop);
                    props.Add(prop);
                }
                
                static void FillInCommonArgumentThings(IArgument arg, Property nukeProp)
                {
                    nukeProp.Default = arg.Default?.Source;
                    nukeProp.Help = arg.Description;
        
                    static string GetName(IArgument arg)
                    {
                        string a = arg.Aliases.FirstOrDefault();
                        if (a is null || !SyntaxFacts.IsValidIdentifier(a))
                        {
                            a = arg.Name;
                            Debug.Assert(SyntaxFacts.IsValidIdentifier(a));
                        }
                        return ToPascalCase(a);
                    }
                    nukeProp.Name = GetName(arg);
                    nukeProp.Type = GetTypeText(arg.TypeInfo.Type);
                    // Can't get this info from runtime argument.
                    nukeProp.Separator = ',';
        
                    // Console.WriteLine();
                }
                
                static string GetTypeTextSimple(Type type)
                {
                    if (type == typeof(float))
                        return "float";
                    if (type == typeof(double))
                        return "double";
                    if (type == typeof(byte))
                        return "byte";
                    if (type == typeof(sbyte))
                        return "sbyte";
                    if (type == typeof(short))
                        return "short";
                    if (type == typeof(ushort))
                        return "ushort";
                    if (type == typeof(int))
                        return "int";
                    if (type == typeof(uint))
                        return "uint";
                    if (type == typeof(long))
                        return "long";
                    if (type == typeof(ulong))
                        return "ulong";
                    if (type == typeof(char))
                        return "char";
                    // if (type == typeof(string))
                    //     return "string";
                    if (type == typeof(bool))
                        return "bool";
                    if (type.IsEnum)
                        return type.Name;
                    return "string";
                }
    
                static string GetTypeText(Type type)
                {
                    if (type.IsGenericType)
                    {
                        if (type.GetGenericTypeDefinition() == typeof(List<>))
                            return $"List<{GetTypeTextSimple(type.GenericTypeArguments[0])}>";
                        if (type.GetGenericTypeDefinition() == typeof(HashSet<>))
                            return $"HashSet<{GetTypeTextSimple(type.GenericTypeArguments[0])}>";
                    }
                    if (type.IsArray)
                        return $"{GetTypeTextSimple(type.GetElementType())}[]";
                    return GetTypeTextSimple(type);
                }
            }
        }
        foreach (var subcommand in command.Subcommands)
            AddNukeTasksRecursively(subcommand, commandTypeNamePrefix, path, context);
    }
}