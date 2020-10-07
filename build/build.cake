#tool "nuget:?package=GitVersion.CommandLine&version=5.3.7"
#tool "nuget:?package=GitReleaseNotes&version=0.7.1"
#tool "nuget:?package=gitreleasemanager&version=0.11.0"
#addin "nuget:?package=Cake.Git&version=0.22.0"

using System.Runtime.InteropServices;

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var _target = Argument("target", "Pack");
var _configuration = Argument("configuration", "Debug");
var _buildNumber = Argument("build-number", 1);
var _nugetFeed = Argument("nuget-feed", (string)null);
var _nugetUserName = Argument("nuget-user-name", (string)null);
var _nugetApiKey = Argument("nuget-api-key", (string)null);
var _pullRequestNumber = Argument("pr-number", (string)null);
var _gitHubPat = Argument("github-pat", (string)null);
var _gitHubRepositoryName = Argument("github-repo-name", (string)null);
var _gitHubRepositoryOwner = Argument("github-repo-owner", (string)null);

///////////////////////////////////////////////////////////////////////////////
// VARIABLES
///////////////////////////////////////////////////////////////////////////////

var _rootDir = Directory("..");
var _srcDir = _rootDir + Directory("src");
var _testsDir = _rootDir + Directory("tests");
var _artifactsDir = _rootDir + Directory("artifacts");
var _solutionFile = GetFiles($"{_rootDir}/*.sln").SingleOrDefault() ??
                    throw new InvalidOperationException("Did not find the solution file");
var _nugetConfigFile = _rootDir + File("nuget.config");

var _defaultMSBuildSettings = new DotNetCoreMSBuildSettings
{
    NoLogo = true,
    Verbosity = DotNetCoreVerbosity.Minimal
};

var _isAzurePipelinesBuild =
    BuildSystem.IsRunningOnAzurePipelines ||
    BuildSystem.IsRunningOnAzurePipelinesHosted;

var _isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

var _gitVersionInfo = _isWindowsOS
    ? GitVersion()
    : GetDummyGitVersionInfo();

var _currentBranch = _gitVersionInfo.BranchName;

var _isCI = !BuildSystem.IsLocalBuild;
var _isMasterBuild = _currentBranch
    .Equals("master", StringComparison.OrdinalIgnoreCase);

var _version = GetLibraryVersion(_gitVersionInfo, _pullRequestNumber, _buildNumber, _isMasterBuild);
var _buildName = GetBuildName(_version, _buildNumber, _isMasterBuild);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(_ =>
{
    Info($"Branch: {_currentBranch}", appendEmptyLine: false);
    Info($"Version: {_version}", appendEmptyLine: false);
    Info($"Configuration: {_configuration}", appendEmptyLine: false);
    Info($"Is CI? {_isCI}", appendEmptyLine: false);
    Info($"Is master build? {_isMasterBuild}", appendEmptyLine: false);

    if (_isAzurePipelinesBuild)
    {
        AzurePipelines.Commands.UpdateBuildNumber(_buildName);
    }
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    Info($"Cleaning {_solutionFile}");

    DotNetCoreClean(_solutionFile.FullPath, new DotNetCoreCleanSettings
    {
        Configuration = _configuration,
        MSBuildSettings = _defaultMSBuildSettings
    });
});

Task("Restore")
    .Does(() =>
{
    Info($"Restoring {_solutionFile}");

    DotNetCoreRestore(_solutionFile.FullPath, new DotNetCoreRestoreSettings
    {
        MSBuildSettings = _defaultMSBuildSettings
    });
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
    Info($"Building {_solutionFile}");

    DotNetCoreBuild(_solutionFile.FullPath, new DotNetCoreBuildSettings
    {
        Configuration = _configuration,
        NoRestore = true,
        MSBuildSettings = _defaultMSBuildSettings
            .SetVersion(_version)
            .WithProperty("ContinuousIntegrationBuild", _isCI.ToString().ToLower())
            .WithProperty("Copyright", $"Copyright © {DateTime.Now.Year} Kato Stoelen")
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    Info($"Running tests in solution {_solutionFile}");

    DotNetCoreTest(_solutionFile.FullPath, new DotNetCoreTestSettings
    {
        Configuration = _configuration,
        NoRestore = true,
        NoBuild = true,
        ArgumentCustomization = args => args.Append("--nologo"),
        Logger = _isAzurePipelinesBuild ? "trx" : null
    });

    if (_isAzurePipelinesBuild)
    {
        AzurePipelines.Commands.PublishTestResults(new AzurePipelinesPublishTestResultsData
        {
            TestRunTitle = _buildName,
            TestRunner = AzurePipelinesTestRunnerType.VSTest,
            Configuration = _configuration,
            TestResultsFiles = GetFiles($"{_testsDir}/**/*.trx").ToList()
        });
    }
});

Task("Clean-Artifacts")
    .Does(() =>
{
    if (DirectoryExists(_artifactsDir))
    {
        Info($"Cleaning artifacts directory {_artifactsDir}");
        CleanDirectory(_artifactsDir);
    }
    else
    {
        Info($"Creating artifacts directory {_artifactsDir}");
        CreateDirectory(_artifactsDir);
    }
});

Task("Pack")
    .IsDependentOn("Build")
    .IsDependentOn("Clean-Artifacts")
    .Does(() =>
{
    Info($"Packing project(s) in solution {_solutionFile}");

    DotNetCorePack(_solutionFile.FullPath, new DotNetCorePackSettings
    {
        Configuration = _configuration,
        OutputDirectory = _artifactsDir,
        NoRestore = true,
        NoBuild = true,
        MSBuildSettings = _defaultMSBuildSettings
    });
});

Task("Push")
    .IsDependentOn("Pack")
    .WithCriteria(!string.IsNullOrEmpty(_nugetFeed), "NuGet feed not specified")
    .Does(() =>
{
    var sourceName = new Uri(_nugetFeed).Host.Replace("www.", string.Empty);

    Info($"Pushing package(s) in {_artifactsDir} to {sourceName}");

    var tempSource = !string.IsNullOrEmpty(_nugetUserName)
        ? new TemporaryNuGetSource(
            sourceName,
            _nugetFeed,
            _nugetUserName,
            _nugetApiKey,
            _nugetConfigFile,
            Context)
        : null;

    try
    {
        NuGetPush(GetFiles($"{_artifactsDir}/*.nupkg"), new NuGetPushSettings
        {
            Source = _nugetFeed,
            ApiKey = _nugetApiKey
        });
    }
    finally
    {
        tempSource?.Dispose();
    }
});

Task("Tag-Commit")
    .WithCriteria(_isMasterBuild, "Not on master branch")
    .Does(() =>
{
    Info($"Creating git tag '{_version}' for commit '{GitLogTip(_rootDir).Sha}'");

    GitTag(_rootDir, _version, "CI", "CI@pipeline.dev", $"Tag version {_version}");
    GitPushRef(_rootDir, "origin", _version);
});

Task("Git-Release")
    .WithCriteria(_isMasterBuild, "Not on master branch")
    .WithCriteria(!string.IsNullOrEmpty(_gitHubPat), "GitHub PAT not specified")
    .WithCriteria(_isWindowsOS, "Only supported on Windows")
    .Does(() =>
{
    Info("Creating release notes");

    var releaseNotesFile = _artifactsDir + File("ReleaseNotes.md");

    GitReleaseNotes(releaseNotesFile, new GitReleaseNotesSettings
    {
        WorkingDirectory = MakeAbsolute(_rootDir),
        RepoToken = _gitHubPat,
        AllLabels = true,
        Version = _version
    });

    Info("Creating GitHub release", prependEmptyLine: true);

    GitReleaseManagerCreate(
        _gitHubPat,
        _gitHubRepositoryOwner,
        _gitHubRepositoryName,
        new GitReleaseManagerCreateSettings
        {
            Name = $"v{_version}",
            InputFilePath = releaseNotesFile,
            Prerelease = !_isMasterBuild,
            TargetCommitish = _currentBranch,
            TargetDirectory = _rootDir,
            Assets = string.Join(",", GetFiles($"{_artifactsDir}/*nupkg").Select(file => file.FullPath))
        });
});

Task("CI")
    .IsDependentOn("Test")
    .IsDependentOn("Push")
    .IsDependentOn("Tag-Commit")
    .IsDependentOn("Git-Release");

RunTarget(_target);

///////////////////////////////////////////////////////////////////////////////
// CUSTOM
///////////////////////////////////////////////////////////////////////////////

private void Info(string message, bool appendEmptyLine = true, bool prependEmptyLine = false)
{
    Console.ForegroundColor = ConsoleColor.Cyan;

    if (prependEmptyLine)
    {
        Information(string.Empty);
    }

    Information(message);

    if (appendEmptyLine)
    {
        Information(string.Empty);
    }

    Console.ResetColor();
}

private string GetLibraryVersion(
    GitVersion gitVersionInfo,
    string pullRequestNumber,
    int buildNumber,
    bool isMasterBuild)
{
    if (isMasterBuild)
    {
        return gitVersionInfo.MajorMinorPatch;
    }

    if (!string.IsNullOrEmpty(pullRequestNumber))
    {
        return $"{gitVersionInfo.MajorMinorPatch}-pr{pullRequestNumber}.{buildNumber}";
    }

    return $"{gitVersionInfo.MajorMinorPatch}-{gitVersionInfo.PreReleaseLabel}.{buildNumber}";
}

private string GetBuildName(string version, int buildNumber, bool isMasterBuild) =>
    isMasterBuild
        ? $"{version} (Build #{buildNumber})"
        : version;

internal class TemporaryNuGetSource : IDisposable
{
    private readonly string _sourceName;
    private readonly string _nugetFeed;
    private readonly string _nugetUserName;
    private readonly string _nugetApiKey;
    private readonly FilePath _nugetConfigFile;
    private readonly ICakeContext _context;

    public TemporaryNuGetSource(
        string sourceName,
        string nugetFeed,
        string nugetUserName,
        string nugetApiKey,
        FilePath nugetConfigFile,
        ICakeContext context)
    {
        _sourceName = sourceName + "_tmp";
        _nugetFeed = nugetFeed;
        _nugetUserName = nugetUserName;
        _nugetApiKey = nugetApiKey;
        _nugetConfigFile = nugetConfigFile;
        _context = context;

        AddSource();
    }

    private void AddSource()
    {
        _context.Information($"Adding temporary NuGet source {_sourceName} with basic auth");

        _context.NuGetAddSource(_sourceName, _nugetFeed, new NuGetSourcesSettings
        {
            UserName = _nugetUserName,
            Password = _nugetApiKey,
            ConfigFile = _nugetConfigFile
        });

        _context.Information(string.Empty);
    }

    private void RemoveSource()
    {
        _context.Information(string.Empty);
        _context.Information($"Removing temporary NuGet source {_sourceName}");

        _context.NuGetRemoveSource(_sourceName, _nugetFeed, new NuGetSourcesSettings
        {
            ConfigFile = _nugetConfigFile
        });
    }

    public void Dispose() => RemoveSource();
}

internal GitVersion GetDummyGitVersionInfo() =>
    new GitVersion
    {
        BranchName = _isAzurePipelinesBuild
            ? EnvironmentVariable("BUILD_SOURCEBRANCH")
            : GitBranchCurrent(_rootDir).FriendlyName,
        MajorMinorPatch = "1.0.0",
        PreReleaseLabel = "unix"
    };