# Auditoría CMS/PAdES — 2026-09-07

## Implementación posterior a la comparación con ReFirma

Se implementó una prueba de compatibilidad en producción para firmas nuevas: `CadesSignatureContainer` completa el ESS de iText con `issuerSerial` antes de la operación privada. `PdfSignatureService` usa `SignExternalContainer`, conserva apariencia/certificado, extensión ESIC, append mode y todos los certificados. El CMS base sigue siendo construido por `PdfPKCS7` en modo CADES; se sustituye su único atributo ESS y se firma una sola vez el SET DER definitivo con el proveedor Windows. No se modifica un PDF ya firmado ni se exporta una clave.

Antes: ESS = AlgorithmIdentifier SHA256 + certHash. Después: los mismos campos + issuerSerial (GeneralNames con directoryName del issuer real y serial ASN.1 del leaf). Se conservan el OID RSA original y SHA256 explícito para aislar esta diferencia respecto a ReFirma. Se comprueba la estructura y hash de la plantilla antes de completarla.

Pruebas ejecutadas: una, dos y tres firmas, con issuerSerial obligatorio para las firmas generadas en la prueba, issuer/serial exactos, ESS único, cadena enviada embebida, hashes ByteRange, cobertura y verificación iText/BouncyCastle. El PDF alterado es rechazado. Compilación Release: cero errores y advertencias. Certificado de prueba efímero; pendiente generar un PDF nuevo con RENIEC y verificarlo en ReFirma 1.6. No se declara resuelto el rechazo externo hasta esa comprobación. Los apartados siguientes documentan la auditoría anterior al cambio.

## Resultado y alcance

Actualización tras recibir la captura: el validador es ReFirma PDF 1.6. Se localizó y auditó `C:\Users\programador_cal\Downloads\documento_firmado2.pdf`. SHA256 del PDF: `000801D5953E93EB60B726E874A22FE48659038E19DF0520EC32BFED64F42BAF`. La auditoría real pasó con una firma, SHA256/RSA, ESS v2 único, leaf embebido, hash ESS y SID coincidentes, ByteRange y cobertura correctos, y verificación criptográfica mediante iText y BouncyCastle. `issuerSerial` está ausente. SHA256 DER del leaf: `B7FB7E8D3569E8A059AE0EEA5E82ADEA2E61A32F2C696720E1E4FC0B585F7878`. La captura confirma el rechazo externo, pero no contiene el diagnóstico interno que permita atribuirlo a issuerSerial. No se cambió el CMS. Esta prueba sin certificado esperado no comprueba que la cadena RENIEC completa esté embebida ni la confianza/revocación de sus certificados.

En la auditoría inicial no había un PDF afectado ni el nombre, versión o diagnóstico del validador. La ausencia de `issuerSerial` no demuestra la causa. Se conserva el CMS de iText; no se declara resuelta la interoperabilidad ni certificada la conformidad ETSI por estas pruebas.

## Flujo encontrado

`Program` descarga el PDF → `CertificateForm` ofrece certificados de CurrentUser/My con clave privada y FIR en el subject → el usuario selecciona un certificado → `OrchestratorService` crea la apariencia → `PdfSignatureService.Sign` crea `PdfSigner` en append mode → `CertificateConverter.ToChain` construye la cadena Windows → `SignDetached(..., CADES)` crea el CMS → `X509Certificate2Signature.Sign` usa `GetRSAPrivateKey().SignData(SHA256, Pkcs1)` → se suben los mismos bytes devueltos por el firmador.

El flujo UI no usa `GetSigningCertificate()` (ese método sí lo usa la utilidad de prueba real). No se exporta la clave privada. `PdfPreparationService` no interviene en este flujo; la apariencia se incorpora antes de finalizar la firma. La descarga local guarda los mismos bytes firmados.

## iText 7.2.5, comprobado en su código fuente

- `PdfSigner.SignDetached` instancia internamente `PdfPKCS7`, calcula el digest sobre `GetRangeStream()`, obtiene los atributos autenticados, solicita su firma al proveedor y serializa el CMS.
- `PdfPKCS7` asigna `signCert = certChain[0]`, incluye la cadena recibida y usa ese mismo certificado para el issuer/serial de SignerInfo y el hash ESS.
- `GetAuthenticatedAttributeSet` agrega una sola instancia de SigningCertificateV2 en modo CADES. Construye AlgorithmIdentifier y certHash, sin ninguna instrucción que agregue issuerSerial. Es comportamiento de la biblioteca, no una pérdida al convertir el serial en la aplicación.
- El issuer procede del TBS del certificado; el serial procede del entero ASN.1 de BouncyCastle. No hay concatenación de DN ni conversión hexadecimal/little endian en ese recorrido.
- SignDetached no ofrece un parámetro para issuerSerial. El método que construye el conjunto es privado y los atributos se reconstruyen al serializar. Cambiar solamente los bytes que recibe IExternalSignature rompería la firma. PdfPKCS7 por sí solo tampoco ofrece ese ajuste. Un contenedor externo permitiría controlarlo, pero exigiría asumir la construcción coherente del CMS; no se justifica sin reproducir el fallo.

## Antes y después

La estructura CMS se conserva:

```text
SignerInfo
├── sid: issuerAndSerialNumber del leaf
└── signedAttrs
    ├── contentType: data
    ├── messageDigest: SHA256(ByteRange)
    └── signingCertificateV2 (una instancia)
        └── certs SEQUENCE OF
            └── ESSCertIDv2
                ├── hashAlgorithm: SHA256 (explícito en iText 7.2.5)
                └── certHash: SHA256(DER del leaf)
```

RFC 5035 permite omitir issuerSerial; recomienda incluirlo y explica las condiciones de identificación. Añadirlo desde el X.509 sería compatible con su sintaxis, pero no demuestra que este validador lo necesite. También deberá examinarse su tratamiento de la codificación del algoritmo DEFAULT explícito si el diagnóstico apunta al parser ASN.1; no se ha demostrado ese fallo.

## Cambio mínimo

`CertificateConverter` ahora rechaza una cadena vacía o cuyo primer DER no sea exactamente el certificado seleccionado. La igualdad del DER cubre subject, issuer, serial, clave pública y SKI cuando exista. No reordena ni elimina certificados.

`X509Chain.Build` ya devolvía los elementos desde el leaf hacia la raíz, pero su resultado booleano se ignora. Una construcción fallida puede indicar confianza, revocación, fechas o una ruta incompleta. No se convierte cualquier resultado false en un rechazo de firma: eso modificaría el funcionamiento con tokens y certificados no confiados localmente. Se conserva la política Windows existente. La aplicación no descarga OCSP/CRL/TSA para incorporarlos al CMS; Windows puede realizar consultas durante Build según su política predeterminada.

## Pruebas ejecutadas

Ejecutar desde la raíz:

```powershell
dotnet run --project tests/FirmadorPades.MultiSignatureTest
```

Las pruebas usan RSA 2048 y un certificado efímero autofirmado con SKI, sin tocar el almacén ni un token. Abren los PDFs con iText y verifican una, dos y tres firmas. Comprueban SubFilter, límites de ByteRange, cobertura completa de cada revisión, cobertura final de la última firma, RSA/SHA256, contentType y messageDigest únicos, ESS v2 único y sin v1, hash del leaf, SID issuer/serial exactos y leaf embebido. Comparan el DER completo con el certificado utilizado y verifican que estén embebidos todos los elementos enviados. Verifican criptográficamente con BouncyCastle y con iText. Un control negativo altera un byte firmado y debe fallar por messageDigest.

Se generan `prueba-1-firmas.pdf`, `prueba-2-firmas.pdf` y `prueba-3-firmas.pdf` en `tests/FirmadorPades.MultiSignatureTest/bin/Debug/net8.0-windows/cms-audit/`.

La prueba autofirmada no acredita una cadena RENIEC completa ni confianza TSL. El auditor puede comprobar issuerSerial cuando exista, sin exigirlo. La apertura por parser no sustituye una prueba visual en Acrobat.

## Prueba real pendiente

1. Firmar un PDF sin firmas con el flujo habitual y el certificado RENIEC seleccionado; descargarlo como `reniec-baseline-b.pdf`.
2. Ejecutar `dotnet run --project tests/FirmadorPades.MultiSignatureTest -- --audit-cms "C:\ruta\reniec-baseline-b.pdf"`.
3. Enviar exactamente ese mismo archivo a ambos validadores y conservar el SHA256 del archivo (`Get-FileHash ... -Algorithm SHA256`) y sus informes detallados.
4. Comprobar PAdES Baseline-B, estado y el diagnóstico de identificación: candidatos, SID, hash ESS, DN/serial, política y versión del motor. El éxito externo exige que el validador problemático deje de devolver NO_SIGNING_CERTIFICATE_FOUND y el otro siga válido.

El mensaje genérico de LTV no demuestra que falten TSA/OCSP/CRL: DSS define ese subestado como imposibilidad de identificar el certificado, distinto de no encontrar una cadena. La hipótesis más compatible con los datos aportados es un comportamiento del procedimiento de identificación/política/parser del validador, pendiente de diagnóstico. No es posible precisar la causa sin el caso real.

## Riesgos

El CMS, apariencia, SHA256/RSA, append mode, versiones y proveedor de clave permanecen iguales. No se espera un cambio de compatibilidad con Acrobat, RENIEC o PAdES-B; no se probaron esos validadores ni hardware. La nueva guarda solo impide continuar si no se puede garantizar el leaf seleccionado. Las firmas múltiples superaron las pruebas locales. No se añadió soporte LT/LTA.

## Fuentes primarias

- [PdfPKCS7.cs, tag 7.2.5, constructor y GetAuthenticatedAttributeSet](https://github.com/itext/itext-dotnet/blob/7.2.5/itext/itext.sign/itext/signatures/PdfPKCS7.cs)
- [PdfSigner.cs, tag 7.2.5, SignDetached](https://github.com/itext/itext-dotnet/blob/7.2.5/itext/itext.sign/itext/signatures/PdfSigner.cs)
- [RFC 5035, sección 5.4.1](https://www.rfc-editor.org/rfc/rfc5035.html#section-5.4.1)
- [DSS: definición de NO_SIGNING_CERTIFICATE_FOUND](https://ec.europa.eu/digital-building-blocks/DSS/webapp-demo/apidocs/eu/europa/esig/dss/enumerations/SubIndication.html)
