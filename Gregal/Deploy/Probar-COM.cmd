@echo off
rem Muestra lo que llega por el puerto COM de la maquina de bingo, sin guardar nada: para probar el cable.
rem Doble clic. Antes cierra la ventana de Gregal (o para el servicio): el puerto solo lo puede abrir un programa.
rem Admite otro puerto o un limite de segundos: Probar-COM COM3 10
cd /d "%~dp0"
"%~dp0Gregal.exe" --escuchar %*
echo.
pause
