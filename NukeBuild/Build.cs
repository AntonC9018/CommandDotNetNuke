using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    readonly AbsolutePath SourceDirectory = RootDirectory / "source";
    readonly AbsolutePath ThirdPartyDirectory = RootDirectory / "third_party";
    readonly AbsolutePath TestsDirectory = RootDirectory / "tests";
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            var binObj = new[] { "**/bin", "**/obj" };
            SourceDirectory.GlobDirectories(binObj).ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories(binObj).ForEach(DeleteDirectory);
            ThirdPartyDirectory.GlobDirectories(binObj).ForEach(DeleteDirectory);
            
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    DotNetBuildSettings ConfigureVersioning(DotNetBuildSettings s) => s
        .SetAssemblyVersion(GitVersion.AssemblySemVer)
        .SetFileVersion(GitVersion.AssemblySemFileVer)
        .SetInformationalVersion(GitVersion.InformationalVersion)
        ;

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s =>
            {
                s = ConfigureVersioning(s);
                return s.SetConfiguration(Configuration)
                    .SetProjectFile(Solution)
                    .EnableNoRestore();
            });
        });

    Project GetConverterProject()
    {
        var project = Solution.GetProject("CommandDotNetNuke.Converter");
        Assert.NotNull(project);
        return project;
    }

    Target BuildPackConverter => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s =>
            {
                s = ConfigureVersioning(s);
                return s
                    .SetConfiguration(Configuration.Release)
                    .EnableNoRestore()
                    .SetProjectFile(GetConverterProject().Path);
            });
        });

    Target PackConverter => _ => _
        .DependsOn(BuildPackConverter)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(GetConverterProject().Path)
                .SetConfiguration(Configuration.Release)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target BuildTestThingy => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution.GetProject("CommandDotNetNuke.Test"))
                .SetProperty("COMPILE_WITH_NUKE_DESCRIBE", "true")
                .SetConfiguration(Configuration)
                // .EnableNoRestore()
                );
        });

    Target NukeGeneratedCodePack => _ => _
        .DependsOn(BuildPackConverter)
        .DependsOn(BuildTestThingy)
        .Executes(() =>
        {
            // if (!TempDirectory.DirectoryExists())
            //     Directory.CreateDirectory(TempDirectory);
            
            // var thingDir = (TempDirectory / "thing");
            // if (thingDir.DirectoryExists())
            //     Directory.Delete(thingDir, recursive: true);
            // Directory.CreateDirectory(thingDir);

            // const string thingProjectName = "project.csproj";
            // var projectThing = thingDir / thingProjectName;

            // var projectTemplatePath = SourceDirectory / "NukeGeneratedCodePackageTemplate" / "PackageTemplate.csproj";
            // File.Copy(projectTemplatePath, projectThing);
            
            var customCodeProject = Solution.GetProject("NukeGeneratedCode");
            Assert.NotNull(customCodeProject);

            DotNetRun(s => s
                .SetProjectFile(Solution.GetProject("CommandDotNetNuke.Test"))
                .SetConfiguration(Configuration)
                .SetProcessWorkingDirectory(customCodeProject.Directory)
                .SetApplicationArguments("nuke-describe")
                .EnableNoRestore()
                .EnableNoBuild());

            DotNetBuild(s => s
                .SetProjectFile(customCodeProject)
                .SetConfiguration(Configuration));
        });
}
