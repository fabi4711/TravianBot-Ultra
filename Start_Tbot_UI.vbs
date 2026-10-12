Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

projectDir = fso.GetParentFolderName(WScript.ScriptFullName)
dotnetExe = ResolveDotnetExe(shell, fso)
projectPath = projectDir & "\src\TbotUltra.Desktop\TbotUltra.Desktop.csproj"
devOutputDir = projectDir & "\temp_build_out\dev-app"
exePath = devOutputDir & "\TbotUltra.Desktop.exe"
sourceStampPath = devOutputDir & "\.source-stamp"
checkStampPath = devOutputDir & "\.ci-check-stamp"
checkLogPath = devOutputDir & "\preflight.log"
checkScriptPath = projectDir & "\scripts\Verify-DevLaunch.ps1"
srcDesktop = projectDir & "\src\TbotUltra.Desktop"
srcWorker = projectDir & "\src\TbotUltra.Worker"
srcCore = projectDir & "\src\TbotUltra.Core"
srcDesktopTests = projectDir & "\src\TbotUltra.Desktop.Tests"
srcWorkerTests = projectDir & "\src\TbotUltra.Worker.Tests"
needsBuild = False

If Len(dotnetExe) = 0 Then
    MsgBox ".NET SDK is missing. Install .NET 8 SDK first.", vbExclamation, "Tbot Ultra"
ElseIf Not fso.FileExists(projectPath) Then
    MsgBox "Desktop project file is missing: " & projectPath, vbExclamation, "Tbot Ultra"
Else
    shell.CurrentDirectory = projectDir

    latestSource = #1/1/2000#
    UpdateLatestModified srcDesktop, latestSource
    UpdateLatestModified srcWorker, latestSource
    UpdateLatestModified srcCore, latestSource
    latestCheckSource = latestSource
    UpdateLatestModified srcDesktopTests, latestCheckSource
    UpdateLatestModified srcWorkerTests, latestCheckSource
    If fso.GetFile(WScript.ScriptFullName).DateLastModified > latestCheckSource Then
        latestCheckSource = fso.GetFile(WScript.ScriptFullName).DateLastModified
    End If
    If fso.FileExists(checkScriptPath) Then
        If fso.GetFile(checkScriptPath).DateLastModified > latestCheckSource Then
            latestCheckSource = fso.GetFile(checkScriptPath).DateLastModified
        End If
    End If

    If Not fso.FileExists(exePath) Then
        needsBuild = True
    ElseIf latestSource > ReadBuildStamp(fso, sourceStampPath) Then
        needsBuild = True
    End If

    If Not fso.FileExists(checkScriptPath) Then
        MsgBox "Preflight script is missing: " & checkScriptPath & ". The app was not started.", vbCritical, "Tbot Ultra - Preflight failed"
        WScript.Quit 1
    End If

    If latestCheckSource > ReadBuildStamp(fso, checkStampPath) Then
        checkCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & checkScriptPath & """ -DotnetExe """ & dotnetExe & """ -LogPath """ & checkLogPath & """"
        checkExitCode = shell.Run(checkCommand, 0, True)
        If checkExitCode <> 0 Then
            MsgBox "Local CI check failed. The app was not started." & vbCrLf & _
                ReadPreflightFailure(fso, checkLogPath) & vbCrLf & vbCrLf & _
                "Full log: " & checkLogPath, vbCritical, "Tbot Ultra - Preflight failed"
            WScript.Quit checkExitCode
        End If
        WriteBuildStamp fso, checkStampPath, latestCheckSource
    End If

    If IsAppRunning(shell) Then
        If Not shell.AppActivate("Tbot Ultra") Then
            MsgBox "Tbot Ultra is already running. Leaving it open; close it before rebuilding the dev app output.", vbInformation, "Tbot Ultra"
        End If
        WScript.Quit 0
    End If

    If needsBuild Then
        buildExitCode = shell.Run("""" & dotnetExe & """ build """ & projectPath & """ -c Debug -nologo -m:1 -o """ & devOutputDir & """ --disable-build-servers -p:NuGetAudit=false -p:UseSharedCompilation=false -p:BuildInParallel=false", 0, True)
        If buildExitCode <> 0 Then
            MsgBox "Build failed. Exit code: " & buildExitCode & ". The app was not started, so you do not accidentally run an old build.", vbExclamation, "Tbot Ultra"
        ElseIf Not fso.FileExists(exePath) Then
            MsgBox "Built exe not found: " & exePath, vbExclamation, "Tbot Ultra"
        Else
            WriteBuildStamp fso, sourceStampPath, latestSource
            LaunchAndActivate shell, exePath
        End If
    Else
        LaunchAndActivate shell, exePath
    End If
End If

Function IsAppRunning(shellObject)
    On Error Resume Next
    Dim exitCode
    exitCode = shellObject.Run("cmd /c tasklist /FI ""IMAGENAME eq TbotUltra.Desktop.exe"" | find /I ""TbotUltra.Desktop.exe"" >nul 2>nul", 0, True)
    IsAppRunning = (exitCode = 0)
End Function

Function ReadPreflightFailure(fileSystemObject, logPath)
    ReadPreflightFailure = "Check the preflight log for the failed step."
    If Not fileSystemObject.FileExists(logPath) Then
        Exit Function
    End If

    Set stream = fileSystemObject.OpenTextFile(logPath, 1)
    Do Until stream.AtEndOfStream
        line = Trim(stream.ReadLine)
        If InStr(1, line, "[FAIL]", vbTextCompare) > 0 Or _
           InStr(1, line, "ERROR:", vbTextCompare) = 1 Or _
           InStr(1, line, "error CS", vbTextCompare) > 0 Then
            ReadPreflightFailure = Left(line, 300)
            Exit Do
        End If
    Loop
    stream.Close
End Function

Sub UpdateLatestModified(folderPath, ByRef latest)
    On Error Resume Next
    If Not fso.FolderExists(folderPath) Then
        Exit Sub
    End If

    Set folder = fso.GetFolder(folderPath)
    folderName = LCase(folder.Name)
    If folderName = "bin" Or folderName = "obj" Then
        Exit Sub
    End If

    ' Directory timestamps do not reliably change when an existing file is edited.
    For Each sourceFile In folder.Files
        If sourceFile.DateLastModified > latest Then
            latest = sourceFile.DateLastModified
        End If
    Next

    For Each subFolder In folder.SubFolders
        UpdateLatestModified subFolder.Path, latest
    Next
End Sub

Function ReadBuildStamp(fileSystemObject, stampPath)
    On Error Resume Next
    ReadBuildStamp = #1/1/2000#
    If Not fileSystemObject.FileExists(stampPath) Then
        Exit Function
    End If

    Set stream = fileSystemObject.OpenTextFile(stampPath, 1)
    value = Trim(stream.ReadLine)
    stream.Close
    If IsDate(value) Then
        ReadBuildStamp = CDate(value)
    End If
End Function

Sub WriteBuildStamp(fileSystemObject, stampPath, sourceModified)
    On Error Resume Next
    Set stream = fileSystemObject.OpenTextFile(stampPath, 2, True)
    stream.Write CStr(sourceModified)
    stream.Close
End Sub

Function ResolveDotnetExe(shellObject, fileSystemObject)
    On Error Resume Next

    Dim preferredPath
    preferredPath = "C:\Program Files\dotnet\dotnet.exe"
    If fileSystemObject.FileExists(preferredPath) Then
        ResolveDotnetExe = preferredPath
        Exit Function
    End If

    Dim whereOutputPath
    whereOutputPath = shellObject.ExpandEnvironmentStrings("%TEMP%") & "\tbot_dotnet_path.txt"
    shellObject.Run "cmd /c where dotnet > """ & whereOutputPath & """ 2>nul", 0, True
    If fileSystemObject.FileExists(whereOutputPath) Then
        Dim stream, resolvedPath
        Set stream = fileSystemObject.OpenTextFile(whereOutputPath, 1)
        resolvedPath = Trim(stream.ReadAll)
        stream.Close
        On Error Resume Next
        fileSystemObject.DeleteFile whereOutputPath, True

        If Len(resolvedPath) > 0 Then
            ResolveDotnetExe = Split(resolvedPath, vbCrLf)(0)
            Exit Function
        End If
    End If

    ResolveDotnetExe = ""
End Function

Sub LaunchAndActivate(shellObject, executablePath)
    On Error Resume Next

    shellObject.Run """" & executablePath & """", 1, False

    Dim attempts
    For attempts = 1 To 20
        WScript.Sleep 250
        If shellObject.AppActivate("Tbot Ultra") Then
            Exit For
        End If
    Next
End Sub
