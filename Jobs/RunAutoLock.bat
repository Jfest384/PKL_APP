@echo off
REM Jalankan query auto-lock ke SQL Server
sqlcmd -S .\SQLEXPRESS -d PKL_APP -E -i "C:\Jobs\AutoLockStudents.sql"