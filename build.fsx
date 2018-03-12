// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let buildDir  = "./build/"
let appReferences = !! "src/**/*.??proj"
let examplesReferences = !! "examples/**/*.??proj"
let dotnetcliVersion = DotNetCli.GetDotNetSDKVersionFromGlobalJson()
let mutable dotnetExePath = "dotnet"
let project = "protoactor-fsharp"
let summary = "F# API for proto.actor"
let configuration = "Release"
let release = LoadReleaseNotes "RELEASE_NOTES.md"
let gitOwner = "mastoj"
let gitName = project
let gitUrl = "https://github.com"
let gitHome = sprintf "%s/%s" gitUrl gitOwner
let outputDir = "./.temp"
// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let run' timeout cmd args dir =
    if execProcess (fun info ->
        info.FileName <- cmd
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
        info.Arguments <- args
    ) timeout |> not then
        failwithf "Error while running '%s' with args: %s" cmd args

let run = run' System.TimeSpan.MaxValue

let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then failwithf "dotnet %s failed" args

// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Description summary
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

Target "Clean" (fun _ ->
    CleanDirs [buildDir; outputDir]
)

Target "InstallDotNetCLI" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "Restore" (fun _ ->
    appReferences
    |> Seq.iter (fun p ->
        let dir = System.IO.Path.GetDirectoryName p
        runDotnet dir "restore"
    )
)

let build fileIncludes _ =
    fileIncludes
    |> Seq.iter (fun p ->
        let dir = System.IO.Path.GetDirectoryName p
        let target = sprintf "build -c %s" configuration
        runDotnet dir target
    )

Target "Build" (build appReferences)

Target "BuildExamples" (build examplesReferences)

Target "NuGet" (fun _ ->
    Paket.Pack(fun p ->
        { p with
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes})
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = outputDir })
)


Target "Release" (fun _ ->
    let user =
        match getBuildParam "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion

    // release on github
    // createClient user pw
    // |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // // TODO: |> uploadFile "PATH_TO_FILE"
    // |> releaseDraft
    // |> Async.RunSynchronously
)

Target "All" DoNothing
Target "BuildPackage" DoNothing

// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------

"AssemblyInfo"
    ==> "Clean"
    ==> "InstallDotNetCLI"
    ==> "Restore"
    ==> "Build"
    ==> "NuGet"
    ==> "BuildPackage"
    ==> "All"

"BuildPackage"
    ==> "PublishNuget"
    ==> "Release"

"Build"
    ==> "BuildExamples"

RunTargetOrDefault "All"
