# DNIe Perú 2.0: detección y firma PC/SC

La aplicación puede leer el certificado de firma directamente del DNIe 2.0,
sin depender de su publicación en `CurrentUser/My` ni del PKCS#11 de IDEMIA.
Se conserva el acceso a certificados Windows, CSP y PKCS#11 existentes.

## Perfil implementado

- ATR comprobado: `3BDC18FF8191FE1FC38073C821136605036351000250`.
- Aplicación: `E828BD080FD25047656E65726963`; certificado de firma: archivo `001D`.
- Lectura pública: SELECT y READ BINARY (`B1`, offset en objeto `54`), con
  decodificación del objeto `53` y extracción de la longitud DER exacta.
- Firma: VERIFY del PIN de firma `81`, MSE `002241B60680018A840181`,
  PSO `002A9E9A` con DigestInfo SHA-256. RSA PKCS#1 v1.5.
- Selección y comandos contrastados con la implementación local de lectura/firma
  de ReFirma PDF 1.6 (`DniePeru2G`); no se invoca ReFirma desde el Firmador.

La lista no solicita PIN ni abre claves privadas. Si hay una copia del certificado
en Windows se conserva preferentemente la procedencia PC/SC. Las otras generaciones
de DNIe no reciben estos comandos: siguen usando las vías anteriores.

## Firma y ciclo de vida

El usuario selecciona el certificado y escribe el PIN en el diálogo del Firmador.
Se conserva en `SecureString` durante el lote y se borra al finalizar. No se registra
ni se transmite al servidor. Cada PDF abre una conexión, vuelve a leer y comparar
el DER con el certificado seleccionado, verifica el PIN una vez, firma y valida
la firma RSA con la clave pública antes de entregar el PDF para su subida.

La conexión y la transacción se liberan antes de subir el documento. Después de
intentar autenticar se reinicializa la tarjeta al liberar la conexión para no dejar
una autenticación activa. El PIN se introduce una vez en la interfaz, aunque se
verifica de nuevo para cada PDF. Un error detiene la sesión y el lote, incluso si
iText envuelve la excepción: no se reintenta el PIN ni se cambia a otro proveedor.

La recuperación de `6985` se intenta una sola vez durante la selección de la
aplicación, antes del PIN. La aplicación no cierra programas ajenos ni reinicia
servicios de Windows. El sondeo automático consulta presencia/ATR y contador de
inserciones, sin enviar APDU. Se pausa durante el diálogo de PIN y la firma.

Las conexiones usan acceso compartido con transacciones PC/SC. Ver
[acceso a tarjetas de Microsoft](https://learn.microsoft.com/en-us/windows/win32/secauthn/smart-card-and-reader-access-functions)
y [reinicialización de conexión](https://learn.microsoft.com/en-us/windows/win32/api/winscard/nf-winscard-scardreconnect).

## Verificación

```powershell
dotnet build --no-restore
dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --test-dnie
dotnet run --project tests/FirmadorPades.MultiSignatureTest
# Solo certificado público; no solicitan PIN ni firman:
dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --read-dnie
dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --list-smart-card
```

El 16/09/2026 se comprobó la lectura con el DNIe conectado y el código compilado.
Las pruebas simuladas cubren bloques y DER truncados, recuperación limitada,
tarjeta distinta/retirada, PIN rechazado/bloqueado, errores de comunicación,
borrado del PIN, GET RESPONSE, verificación RSA y dos firmas CMS/PDF incrementales.
Se corrigió una expectativa antigua de la prueba de posición del sello: debe
comprobar la página solicitada, no siempre la última página.

Queda pendiente una firma con el PIN real y su validación en ReFirma. Las pruebas
simuladas no demuestran por sí solas la firma física ni la validación externa.
Para esa prueba se puede usar la interfaz habitual o `--sign-once-real entrada.pdf
salida.pdf` desde una consola interactiva (PIN oculto; nunca como argumento).
