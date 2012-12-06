@echo off
::
:: requires netcat!
::
setlocal

set wait_for_server=server\instance
set wait_for_user=some_username
set notify_email=user@host.com
set notify_smtp=mail_server

:: Wait for processes
sqlwaitfor -S %wait_for_server% -E -L %wait_for_user% -v

:: Send email
echo Notifying DBA...
call :create_tmp_file
set tmp1=%ret%
>  %tmp1% echo EHLO me
>> %tmp1% echo MAIL FROM: dba-notification@host.com
>> %tmp1% echo RCPT TO: %notify_email%
>> %tmp1% echo DATA
call :create_tmp_file
set tmp2=%ret%
>> %tmp2% echo From: "DBA Notification" ^<dba-notification@host.com^>
>> %tmp2% echo To: ^<%notify_email%^>
>> %tmp2% echo Subject: Processes complete for %wait_for_user%
>> %tmp2% echo.
>> %tmp2% echo This email has been sent to notify you that the SQL Server processes you were
>> %tmp2% echo waiting for have been completed.
>> %tmp2% echo .
>> %tmp2% echo.
>> %tmp2% echo QUIT
(type %tmp1% && sleep 2 && type %tmp2%) | nc %notify_smtp% 25
del %tmp1% %tmp2%
goto :eof

:create_tmp_file
    set ret=
    set ext=%1
    if "%ext%" == "" set ext=tmp
    for /f "tokens=2-8 delims=/:. " %%a in ("%date%:%time: =0%") do set "ret=%temp%\tmp_%%c%%a%%b%%d%%e%%f%%g.%ext%"
    if exist %ret% del %ret%
    set ext=
    goto :eof
