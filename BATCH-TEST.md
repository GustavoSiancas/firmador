# Prueba local de firma masiva

Ejecutar la aplicación sin argumentos o con `--batch-test`:

```powershell
dotnet run -- --batch-test
```

1. Agregar entre 1 y 20 PDFs. Se pueden quitar archivos antes de firmar.
2. Elegir un certificado. Usar «Actualizar certificados» después de conectar el dispositivo.
3. Opcionalmente activar «Quitar firmas anteriores (clean)»; está desactivado inicialmente.
4. Pulsar «Firmar todos» e ingresar el PIN en el formulario del lote. El mismo certificado firma cada PDF, con el sello en la última página.
5. Revisar el resultado por archivo y guardar los PDFs firmados en un ZIP.

Los originales permanecen intactos. Este modo no descarga ni sube documentos a endpoints.
Si un archivo falla, los demás continúan y el resultado muestra el error.
El ZIP incluye solo los documentos firmados y numera los nombres para evitar colisiones.
Los resultados se mantienen en memoria hasta cerrar el formulario de resultados.
El formulario local utiliza IDEMIA IDPlugClassic mediante PKCS#11. Requiere su
biblioteca `IDEMIA/IDPlugClassic/DLLs/idplug-pkcs11.dll` instalada en Program Files
con la misma arquitectura que la aplicación. No utiliza la clave privada de Windows.
Antes de autenticar, compara el certificado completo del token con el seleccionado
en Windows y busca su clave privada por CKA_ID. Mantiene una sesión para todo el lote.

El PIN se recibe en un formulario que muestra solo caracteres de máscara y se
mantiene en un SecureString durante el lote. Se usa para C_Login; si la clave tiene
CKA_ALWAYS_AUTHENTICATE, se suministra también para la autenticación de cada firma.
Las copias temporales del PIN se borran después de usarlas. Se cierra la sesión
y se libera la copia de la aplicación al terminar. No se guarda en disco ni registros.
Ante un error criptográfico se detiene el lote sin reintentar automáticamente el PIN.
Cada firma se verifica con la clave pública del certificado antes de devolverla.
La firma real y la ausencia de diálogos deben comprobarse con el DNIe y su PIN.

Diagnóstico sin PIN ni firma:

```powershell
dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --probe-pkcs11
```

El inicio con una URL `firmaapp://` conserva el flujo por endpoints.
