#:sdk Cake.Sdk@6.0.0
#:package Cake.GitVersioning@3.9.50

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var staging = Argument("staging", "./stg");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("SetBuildVersion")
    .WithCriteria(!BuildSystem.IsLocalBuild && target == "PackAndPush")
    .Does(() =>
    {
        GitVersioningCloud(".", new GitVersioningCloudSettings
        {
            CloudBuildNumber = true,
        });
    });

Task("Restore")
    .Does(() =>
    {
       DotNetRestore("./DevTools.slnx");
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetBuild("./DevTools.slnx", new DotNetBuildSettings
        {
            Configuration = configuration,
        });
    });

Task("PackAndPush")
    .IsDependentOn("SetBuildVersion")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetPack("./src/DevTools/DevTools.csproj", new DotNetPackSettings
        {
            NoRestore = true,
            NoBuild = true,
            Configuration = configuration,
            OutputDirectory = staging,
        });

        foreach(var nuget in GetFiles($"{staging}/*.nupkg"))
        {
            DotNetNuGetPush(nuget, new DotNetNuGetPushSettings
            {
                ApiKey = EnvironmentVariable("NUGET_API_KEY"),
                Source = EnvironmentVariable("NUGET_SOURCE_URL"),
            });
        }
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target); 