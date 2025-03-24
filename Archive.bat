@echo off
echo Deleting folders bin, obj, and output...

:: Loop through all folders and subfolders containing bin, obj, and output
for /r %%d in (bin obj output) do (
    if exist "%%d" (
        echo Deleting folder: %%d
        rd /s /q "%%d"
    )
)

echo Deletion completed.

:: Get the name of the current directory
for %%I in ("%cd%") do set "foldername=%%~nxI"

:: Check if an archive with the same name exists
if exist "%foldername%.rar" (
    echo Archive "%foldername%.rar" already exists, deleting it...
    del /q "%foldername%.rar"
)

:: Создаем временную директорию
echo Создание временной директории...
set "tempdir=D:\project_archive"
mkdir "%tempdir%"

:: Копируем файлы во временную директорию, исключая папку .git
echo Копирование файлов во временную директорию, исключая папку .git...
robocopy "%cd%" "%tempdir%" /E /XD ".git"

:: Архивируем временную директорию
echo Архивирование временной директории...
"C:\Program Files\WinRAR\WinRAR.exe" a -r "%foldername%.rar" "%tempdir%\*"

:: Удаляем временную директорию
echo Очистка...
rmdir /s /q "%tempdir%"

echo Архивирование завершено.
pause