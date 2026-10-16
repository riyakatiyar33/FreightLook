@echo off
REM Runs all FreightLook scripts in order. Usage: 00_RunAll.cmd [server]
REM Default server: (localdb)\MSSQLLocalDB
set SERVER=%~1
if "%SERVER%"=="" set SERVER=(localdb)\MSSQLLocalDB
pushd "%~dp0"
for %%f in (01_CreateDatabase.sql 02_Tables.sql 03_Types.sql 04_StoredProcedures.sql 05_SeedData.sql) do (
    echo Running %%f ...
    sqlcmd -S "%SERVER%" -E -b -I -i "%%f" || (popd & exit /b 1)
)
popd
echo Done.
