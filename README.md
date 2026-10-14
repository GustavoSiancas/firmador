# Firmador CAL

## Formas de inicio

- **Abrir la aplicación sin parámetros:** abre la ventana de firma independiente. Agregue uno o varios PDF, revise la vista previa, seleccione el certificado y pulse «Firmar y guardar ZIP». Elija el destino e introduzca el PIN. Este modo trabaja localmente, sin API ni token.
- **Inicio desde el sistema web:** mediante `firmaapp` con `sessionEndpoint` y `token`, procesados por `LaunchService`. Conserva la descarga de documentos y la actualización de los PDF firmados en el servidor.

La ventana independiente organiza los documentos, el certificado y la ubicación de firma en secciones separadas. El botón con icono de matriz permite elegir una de 12 posiciones (3 columnas × 4 filas), además de la primera o última página. La selección se aplica a todo el lote y actualiza la vista previa. Por defecto, se utiliza la esquina inferior derecha de la última página. Puede agregar más PDF o quitar archivos antes de firmar.

Los originales no se modifican. El ZIP incluye los PDF firmados con nombres únicos y un archivo `errores.txt` si hay fallos. También se genera cuando fallan todos los PDF; en ese caso contiene el reporte de errores. Ante un error criptográfico se detiene el lote sin reintentar el PIN.

## Instalador

`compiler.iss` empaqueta la distribucion `bin/distribution-optimized/win-x64-1.12` e incluye IDPlug y RENIEC DNIe desde `extra/`. Requiere permisos de administrador, instala los dos componentes antes del Firmador y suprime sus reinicios individuales. Al finalizar solicita reiniciar Windows, con la opcion de hacerlo mas tarde. Si un componente falla o se cancela, muestra el error y permite reintentar antes de continuar. Los registros de los componentes se guardan en `%TEMP%` como `FirmadorCAL-IDPlug.log` y `FirmadorCAL-RENIEC.log`.

Compile `compiler.iss` con Inno Setup para generar `bin/installer/FirmadorCALSetup-1.12.exe`. Para instalaciones silenciosas, agregue `/NORESTART` al instalador principal si el reinicio se gestionara externamente.

## Verificación de la aplicación

```powershell
dotnet build --no-restore
dotnet run --project tests/FirmadorPades.MultiSignatureTest --no-restore
```

Las pruebas incluyen las 12 ubicaciones en la primera y última página, la conservación de los PDF originales, la salida a ZIP con nombres repetidos, las firmas resultantes y el ZIP de un lote fallido. Para renderizar las ventanas sin acceder a certificados:

```powershell
dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --render-local-ui tmp/ui-review
```

Estas verificaciones no reemplazan la firma con un DNIe físico.
