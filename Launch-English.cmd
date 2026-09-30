@echo off
setlocal
cd /d "%~dp0"
set "EXILEAPI_LANGUAGE=en-US"
start "" "%~dp0Loader.exe" %*
