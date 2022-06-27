using System;
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
    // [GitRepository] readonly GitRepository GitRepository;
    // [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ThirdPartyDirectory => RootDirectory / "third_party";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

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
                .SetProperty("RootDirectory", RootDirectory)
                .SetProjectFile(Solution));
        });

    DotNetBuildSettings ConfigureVersioning(DotNetBuildSettings s) => s
        // .SetAssemblyVersion(GitVersion.AssemblySemVer)
        // .SetFileVersion(GitVersion.AssemblySemFileVer)
        // .SetInformationalVersion(GitVersion.InformationalVersion)
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

    Target Pack => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var project = Solution.GetProject("CommandDotNetNuke.Converter");
            Assert.NotNull(project);

            DotNetBuild(s =>
            {
                s = ConfigureVersioning(s);
                s = s.SetConfiguration(Configuration.Release);
                s = s.EnableNoRestore();
                s = s.SetProjectFile(project.Path);
                s = s.SetProperty("RootDirectory", RootDirectory);
                return s;
            });
            
            DotNetPack(s => s
                .SetProperty("RootDirectory", RootDirectory)
                .SetProject(project.Path)
                .SetConfiguration(Configuration.Release)
                .EnableNoBuild()
                .EnableNoRestore());
        });
}
