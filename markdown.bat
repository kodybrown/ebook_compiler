@setlocal
@echo off

rem If needed, replace "set perlexe=perl.exe" below with
rem "set perlexe=C:\Perl64\bin\perl.exe" using the path to
rem your perl installation instead. The default Windows
rem installation path is 'C:\Perl64\bin\perl.exe'.

set perl_exe=C:\Tools\Perl64\bin\perl.exe
set ERR=0

if not exist "%perl_exe%" (
    echo Could not find perl.exe.
    echo Please fix your PATH environment variable,
    echo or include the full path to it in the
    echo %~0 batch file. See batch file for more details.
    rem See comments above starting on line 4.
    pause
    @endlocal && exit /B 2
)

pushd "%~dp0"

if not "%2"=="" (
    "%perl_exe%" "%~dp0markdown.pl" --html4tags "%~1" > "%~2"
) else if not [%1]==[] (
    "%perl_exe%" "%~dp0markdown.pl" --html4tags "%~1" > "%~dpn1.html"
) else (
    call :usage
    set ERR=1
    pause
)

popd

@endlocal && exit /B %ERR%

:usage
    echo simple wrapper around markdown.pl
    echo.
    echo usage:
    echo   %~nx0 ^<source-file^> [^<output-file^>]
    echo.
    echo     if ^<output-file^> is not specified, the ^<source-file^> name will be used,
    echo     replacing its extension with `.html`.
    goto :eof
