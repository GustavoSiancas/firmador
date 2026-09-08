# Prueba local de firma masiva

Ejecutar la aplicación sin argumentos o con `--batch-test`:

```powershell
dotnet run -- --batch-test
```

1. Agregar entre 1 y 20 PDFs. Se pueden quitar archivos antes de firmar.
2. Elegir un certificado. Usar «Actualizar certificados» después de conectar el dispositivo.
3. Opcionalmente activar «Quitar firmas anteriores (clean)»; está desactivado inicialmente.
4. Pulsar «Firmar todos». El mismo certificado firma cada PDF, con el sello en la última página.
5. Revisar el resultado por archivo y guardar los PDFs firmados en un ZIP.

Los originales permanecen intactos. Este modo no descarga ni sube documentos a endpoints.
Si un archivo falla, los demás continúan y el resultado muestra el error.
El ZIP incluye solo los documentos firmados y numera los nombres para evitar colisiones.
Los resultados se mantienen en memoria hasta cerrar el formulario de resultados.
El formulario solicita el PIN una vez para el lote y firma mediante IDEMIA
IDPlugClassic (PKCS#11), sin usar el proveedor de claves de Windows.
Requiere idplug-pkcs11.dll instalado en IDEMIA/IDPlugClassic/DLLs bajo Program Files.
El PIN permanece en memoria durante el lote; las copias temporales se borran y
la copia del formulario se libera al terminar o fallar. No se guarda en disco.
La misma sesion se reutiliza; si la tarjeta requiere autenticacion por operacion,
se suministra el PIN a PKCS#11 automaticamente. Cada firma se verifica con el
certificado elegido. Ante errores criptograficos se detiene el lote sin reintentar.
La ausencia de dialogos depende del comportamiento de la biblioteca del DNIe.

Si al abrir PKCS#11 la tarjeta no se reconoce (CKR_TOKEN_NOT_RECOGNIZED),
la biblioteca no puede cargarse o la función/mecanismo no está soportado,
el lote cambia automáticamente al proveedor de Windows para firmar cada PDF.
El formulario indica el cambio y libera el PIN recibido; la autenticación pasa
a Seguridad de Windows. Un PIN incorrecto, bloqueado o una cancelación no
activan este respaldo. La generación del DNIe no se deduce del error.

El inicio con una URL `firmaapp://` conserva el flujo por endpoints.
