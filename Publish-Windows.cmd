@echo off
cd /d "%~dp0"
dotnet publish "KitoIconStudio\KitoIconStudio.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish"
if errorlevel 1 (
    echo.
    echo Publish failed. Check .NET 10 SDK / Visual Studio installation.
    pause
    exit /b 1
)
echo.
echo Ready: publish\KitoIconStudio.exe
pause
