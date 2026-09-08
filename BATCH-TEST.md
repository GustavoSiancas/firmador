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
La firma de cada PDF utiliza el proveedor de Windows, que muestra su di?logo de
autenticaci?n cuando corresponde. La aplicaci?n no captura ni almacena el PIN.
El acceso a la clave privada se abre y se libera por cada documento.
Ante un error criptogr?fico se detiene el lote; los PDFs ya firmados se pueden guardar.

El inicio con una URL `firmaapp://` conserva el flujo por endpoints.
