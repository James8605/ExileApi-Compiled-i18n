@echo off
setlocal
cd /d "%~dp0"
set "EXILEAPI_LANGUAGE=zh-CN"
start "" "%~dp0Loader.exe" %*
