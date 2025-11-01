@echo off
sqlcmd -S .\SQLEXPRESS -d PKL_APP -E -i "C:\Jobs\ResetReport.sql"