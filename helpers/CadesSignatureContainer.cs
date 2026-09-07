using System.Security.Cryptography;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;

namespace FirmadorPades.Helpers;

/// <summary>Conserva el CMS de iText y completa su referencia ESS antes de firmar.</summary>
internal sealed class CadesSignatureContainer : IExternalSignatureContainer
{
    private readonly IExternalSignature _signature;
    private readonly X509Certificate[] _chain;

    public CadesSignatureContainer(IExternalSignature signature, X509Certificate[] chain)
    {
        _signature = signature;
        _chain = chain;
    }

    public void ModifySigningDictionary(PdfDictionary dictionary)
    {
        dictionary.Put(PdfName.Filter, PdfName.Adobe_PPKLite);
        dictionary.Put(PdfName.SubFilter, PdfName.ETSI_CAdES_DETACHED);
    }

    public byte[] Sign(Stream data)
    {
        byte[] digest = SHA256.HashData(data);
        var pkcs7 = new PdfPKCS7(null, _chain, DigestAlgorithms.SHA256, false);
        // Plantilla de iText, aún sin firma privada ni PDF finalizado.
        pkcs7.SetExternalDigest(Array.Empty<byte>(), null, "RSA");
        byte[] template = pkcs7.GetEncodedPKCS7(digest, PdfSigner.CryptoStandard.CADES, null, null, null);
        var content = ContentInfo.GetInstance(Asn1Object.FromByteArray(template));
        var signedData = SignedData.GetInstance(content.Content);
        var signerInfo = SignerInfo.GetInstance(signedData.SignerInfos[0]);
        var attributes = new Asn1EncodableVector();
        int references = 0;
        foreach (Asn1Encodable item in signerInfo.AuthenticatedAttributes)
        {
            var attribute = Org.BouncyCastle.Asn1.Cms.Attribute.GetInstance(item);
            if (attribute.AttrType.Id == "1.2.840.113549.1.9.16.2.47")
            {
                references++;
                // Conserva el AlgorithmIdentifier y certHash originales de iText.
                var signingCertificate = Asn1Sequence.GetInstance(attribute.AttrValues[0]);
                var certs = Asn1Sequence.GetInstance(signingCertificate[0]);
                var ess = Asn1Sequence.GetInstance(certs[0]);
                if (attribute.AttrValues.Count != 1 || signingCertificate.Count != 1 || certs.Count != 1 ||
                    ess.Count != 2 ||
                    AlgorithmIdentifier.GetInstance(ess[0]).Algorithm.Id != "2.16.840.1.101.3.4.2.1" ||
                    !Asn1OctetString.GetInstance(ess[1]).GetOctets().AsSpan()
                        .SequenceEqual(SHA256.HashData(_chain[0].GetEncoded())))
                    throw new InvalidOperationException("La referencia ESS de iText no corresponde al certificado firmante SHA256.");
                var issuerSerial = new IssuerSerial(
                    new GeneralNames(new GeneralName(_chain[0].IssuerDN)),
                    new DerInteger(_chain[0].SerialNumber));
                var completeEss = new DerSequence(ess[0], ess[1], issuerSerial);
                attribute = new Org.BouncyCastle.Asn1.Cms.Attribute(attribute.AttrType,
                    new DerSet(new DerSequence(new DerSequence(completeEss))));
            }
            attributes.Add(attribute);
        }
        if (references != 1)
            throw new InvalidOperationException("Se esperaba una única referencia SigningCertificateV2 de iText.");

        var signedAttributes = new DerSet(attributes);
        // Una sola operación RSA mediante el proveedor Windows/token, sobre el SET DER final.
        byte[] signature = _signature.Sign(signedAttributes.GetDerEncoded());
        var completedSigner = new SignerInfo(signerInfo.SignerID, signerInfo.DigestAlgorithm,
            signedAttributes, signerInfo.DigestEncryptionAlgorithm,
            new DerOctetString(signature), signerInfo.UnauthenticatedAttributes);
        var completedData = new SignedData(signedData.DigestAlgorithms, signedData.EncapContentInfo,
            signedData.Certificates, signedData.CRLs, new DerSet(completedSigner));
        return new ContentInfo(content.ContentType, completedData).GetDerEncoded();
    }
}
