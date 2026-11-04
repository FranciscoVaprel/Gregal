@echo off
rem Configura Gregal: puerto COM, sala, fabricante, base de datos, contrasena y como arranca.
rem Doble clic, o escribir "Configurar" en la carpeta de Gregal. Pide permisos de administrador.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-Gregal.ps1" -Pausar %*
