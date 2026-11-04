@echo off
rem Busca en GitHub una version nueva de Gregal y la instala (conserva la configuracion).
rem Doble clic, o escribir "Actualizar" en la carpeta de Gregal.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Buscar-Actualizacion.ps1" %*
