@ECHO OFF
REM			This file 
REM				1) will download the official HdrHistogram github repo
REM				2) extract the downloaded zip
REM				3) build the C# code
REM				4) copy the compiled DLL to the ..\lib\ folder ([root]\src\HdrHistogram.PerfTestOrg\lib\HdrHistogram.dll)
REM			It assumes the following dependencies are available:
REM				* Powershell																		- http://technet.microsoft.com/en-gb/library/hh847837.aspx
REM				* .NET 4.5 (for Zipping/Unzipping)													- http://www.microsoft.com/en-gb/download/details.aspx?id=42642

SET CurrDir=%~dp0
SET DownloadFilePsPath=%CurrDir%Download-File.ps1
SET DownloadUrl=https://github.com/HdrHistogram/HdrHistogram/archive/master.zip
SET TargetDownloadFolderPath=%CurrDir%OfficialRepo
SET TargetLibPath=%cd%\..\lib

:Download & extract
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& '%DownloadFilePsPath%' -address '%DownloadUrl%' -destination '%TargetDownloadFolderPath%'"

:Build official C# code base
PUSHD OfficialRepo\HdrHistogram-master\src\main\csharp\
	ECHO ----Attempting to compile official build -----
	MSBuild HdrHistogram.NET.csproj /p:Configuration=Release

	ECHO ----Attempting to compile official build -----	
	ROBOCOPY bin\release\ %TargetLibPath%
POPD

RD /q /s OfficialRepo

