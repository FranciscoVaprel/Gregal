@echo off
rem Vuelve a guardar en la base de datos las partidas que quedaron en logs\historico_pendientes_tag.jsonl
rem porque Firebird fallo en su momento. Doble clic; se puede hacer con Gregal en marcha.
cd /d "%~dp0"
"%~dp0Gregal.exe" --reimportar-pendientes
echo.
pause
