@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

set DP0=%~dp0
set N0=%~n0

set BUILD_PLATFORM=Win32
set RUNMODE=cpp

set PROTOC_OUTDIR=%DP0%\rpc
set PROTO_DIR=%DP0%\proto
set PROTOFILE=*.proto

if "_%1"=="_/?" goto :usage
if "_%1"=="_--help" goto :usage
if "_%1"=="_-h" goto :usage

if not _%1==_ set RUNMODE=%1
if not _%2==_ set PROTO_DIR=%~f2
if not _%3==_ set PROTOC_OUTDIR=%~f3
if not _%4==_ set BUILD_CONFIG=%4
if not _%5==_ set BUILD_PLATFORM=%5
if not _%6==_ set PROTOFILE=%6

set ADDITIONAL_PROTOC_COMMAND=%7
:loop
shift
if [%7]==[] goto afterloop
set ADDITIONAL_PROTOC_COMMAND=%ADDITIONAL_PROTOC_COMMAND% %7
goto loop
:afterloop

rem Remove extra quotes
set PROTO_DIR=%PROTO_DIR:"=%
set PROTOC_OUTDIR=%PROTOC_OUTDIR:"=%
set BUILD_CONFIG=%BUILD_CONFIG:"=%
set BUILD_PLATFORM=%BUILD_PLATFORM:"=%
set PROTOFILE=%PROTOFILE:"=%

if _%BUILD_PLATFORM%==_ (
	set BUILD_PLATFORM=Win32
)
call :trace Build platform is %BUILD_PLATFORM%
set "VALID_PLATFORM_FLAG="
if _%BUILD_PLATFORM%==_Win32 set VALID_PLATFORM_FLAG=1
if _%BUILD_PLATFORM%==_x64 set VALID_PLATFORM_FLAG=1
if not defined VALID_PLATFORM_FLAG (
	call :trace Invalid platform [%BUILD_PLATFORM%]
	goto :usage
)



set PREBUILT_DIR=%DP0%\..\prebuilt\windows\%BUILD_CONFIG%\%BUILD_PLATFORM%\
set PROTOC=%PREBUILT_DIR%\protoc.exe


if NOT EXIST "%PROTOC%" (
	echo error : Could not find protoc.exe in %PROTOC%
	exit /b 1
)
call :trace protoc.exe at %PROTOC%
call :trace RunMode is %RUNMODE%
set PROTOC_OUT_FLAG=%RUNMODE%_out
set GRPC_PLUGIN_NAME=grpc_%RUNMODE%_plugin

call :trace protoc.exe at %PROTOC%
call :trace .proto files at %PROTO_DIR%
call :trace protoc.exe output directory is %PROTOC_OUTDIR%
mkdir "%PROTOC_OUTDIR%"
set PROTOC_TEMP_OUTDIR=%PROTOC_OUTDIR%\%BUILD_PLATFORM%_%BUILD_CONFIG%
mkdir "%PROTOC_TEMP_OUTDIR%"
for /f "usebackq" %%p in (`dir /b "%PROTO_DIR%\%PROTOFILE%"`) do (
	call :trace Generating files for proto %%p
	
	set command="%PROTOC%" -I "%PROTO_DIR%" "--%PROTOC_OUT_FLAG%=%PROTOC_TEMP_OUTDIR%" "%PROTO_DIR%\%%p" %ADDITIONAL_PROTOC_COMMAND%
	call :trace !command!
	cmd /s /c ^"!command!^"
	
	if ERRORLEVEL 1 (
		echo %%p: error : protoc returned an error
		call :error_external_command_output !command! --error_format=msvs
		exit /b 1
	)	
	
	set command="%PROTOC%" -I "%PROTO_DIR%" "--grpc_out=%PROTOC_TEMP_OUTDIR%" "--plugin=protoc-gen-grpc=%PREBUILT_DIR%\%GRPC_PLUGIN_NAME%.exe" "%PROTO_DIR%\%%p" %ADDITIONAL_PROTOC_COMMAND%
	call :trace !command!
	cmd /s /c ^"!command!^"
	
	if ERRORLEVEL 1 (
		echo %%p: error : protoc returned an error
		call :error_external_command_output !command! --error_format=msvs
		exit /b 1
	)	
)

set PROTONAME=%PROTOFILE:.proto=%
call :trace patching files for proto %PROTONAME%
if _%RUNMODE%==_cpp (
	for %%p in ("%PROTOC_TEMP_OUTDIR%\*%PROTONAME%*.h" "%PROTOC_TEMP_OUTDIR%\*%PROTONAME%*.cc") do (
		rem temp_file has to be unique to support parallel build
		set temp_file=%%p.%BUILD_CONFIG%.%BUILD_PLATFORM%.back
		call :trace patching "%%p" with "!temp_file!"
		move "%%p" "!temp_file!" > NUL
		copy /b "%DP0%\h_file_header"+"!temp_file!"+"%DP0%\h_file_footer" "%%p" > NUL
		del /Q "!temp_file!" > NUL
		call :trace Copying "%%p" to "%PROTOC_OUTDIR%"
		copy /y "%%p" "%PROTOC_OUTDIR%"
	)
)
if _%RUNMODE%==_csharp (
	set CSPROTONAME=%PROTONAME:_=%
	for %%p in ("%PROTOC_TEMP_OUTDIR%\*%CSPROTONAME%*.cs") do (
		copy /y "%%p" "%PROTOC_OUTDIR%"
	)
)
if _%RUNMODE%==_python (
	for %%p in ("%PROTOC_TEMP_OUTDIR%\*%PROTONAME%_pb2*.py") do (
		copy /y "%%p" "%PROTOC_OUTDIR%"
	)
)

exit /b 0

:usage
echo Usage: %N0% ^<cpp^|csharp^|python^> ^<proto dir^> ^<output dir^> [Debug^|Release]
exit /b 1

:trace
echo [ %DATE% %TIME% ] %*
exit /b 0

:error_external_command_output
SET COMMAND_OUTPUT=
SET COMMAND="%DP0%\run_command.bat" %*
for /F "usebackq delims=" %%i in (`^"%COMMAND%^"`) do SET COMMAND_OUTPUT=!COMMAND_OUTPUT=! %%i

set COMMAND_OUTPUT=%COMMAND_OUTPUT:) : error=) : error PROTOBUF:%

echo %COMMAND_OUTPUT%

exit /b 0

