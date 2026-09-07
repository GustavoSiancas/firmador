using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.Ess;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509;

internal static class CmsAudit
{
    public static void Verify(byte[] pdf, X509Certificate2? expected = null)
    {
        using var document = new PdfDocument(new PdfReader(new MemoryStream(pdf)));
        var util = new SignatureUtil(document);
        var names = util.GetSignatureNames();
        Require(names.Count > 0, "No hay firmas");
        foreach (string name in names)
        {
            var dictionary = util.GetSignatureDictionary(name);
            Require(PdfName.ETSI_CAdES_DETACHED.Equals(dictionary.GetAsName(PdfName.SubFilter)), "SubFilter");
            var range = dictionary.GetAsArray(PdfName.ByteRange);
            Require(range.Size() == 4, "ByteRange: cantidad");
            long[] r = Enumerable.Range(0, 4).Select(i => range.GetAsNumber(i).LongValue()).ToArray();
            Require(r[0] == 0 && r[1] > 0 && r[2] > r[1] && r[3] >= 0 && r[2] <= pdf.Length && r[3] <= pdf.Length - r[2], "ByteRange: límites");
            Require(pdf[(int)r[1]] == '<' && pdf[(int)r[2] - 1] == '>', "ByteRange: hueco Contents");
            if (name == names.Last())
                Require(r[2] + r[3] == pdf.Length && util.SignatureCoversWholeDocument(name), "Cobertura final");
            using var revision = util.ExtractRevision(name);
            using var revisionPdf = new PdfDocument(new PdfReader(revision));
            Require(new SignatureUtil(revisionPdf).SignatureCoversWholeDocument(name), "Cobertura de revisión");

            using var content = new MemoryStream();
            content.Write(pdf, 0, checked((int)r[1]));
            content.Write(pdf, checked((int)r[2]), checked((int)r[3]));
            byte[] signedBytes = content.ToArray();
            using var asn = new Asn1InputStream(dictionary.GetAsString(PdfName.Contents).GetValueBytes());
            byte[] encoded = asn.ReadObject().GetEncoded();
            var data = SignedData.GetInstance(ContentInfo.GetInstance(Asn1Object.FromByteArray(encoded)).Content);
            Require(data.SignerInfos.Count == 1, "SignerInfo único");
            Require(data.EncapContentInfo.Content == null, "CMS detached");
            var info = SignerInfo.GetInstance(data.SignerInfos[0]);
            Require(!info.SignerID.IsTagged, "SID issuerAndSerialNumber");
            var sid = IssuerAndSerialNumber.GetInstance(info.SignerID.ID);
            var attributes = info.AuthenticatedAttributes.Cast<Asn1Encodable>()
                .Select(Org.BouncyCastle.Asn1.Cms.Attribute.GetInstance).ToArray();
            var essAttributes = attributes.Where(a => a.AttrType.Id == "1.2.840.113549.1.9.16.2.47").ToArray();
            Require(essAttributes.Length == 1 && essAttributes[0].AttrValues.Count == 1, "SigningCertificateV2 único");
            Require(!attributes.Any(a => a.AttrType.Id == "1.2.840.113549.1.9.16.2.12"), "Sin SigningCertificate v1");
            var ess = SigningCertificateV2.GetInstance(essAttributes[0].AttrValues[0]).GetCerts()[0];
            const string sha256 = "2.16.840.1.101.3.4.2.1";
            Require(ess.HashAlgorithm.Algorithm.Id == sha256 && info.DigestAlgorithm.Algorithm.Id == sha256, "SHA-256");
            Require(info.DigestEncryptionAlgorithm.Algorithm.Id == "1.2.840.113549.1.1.1", "RSA PKCS#1");
            var digestAttributes = attributes.Where(a => a.AttrType.Equals(CmsAttributes.MessageDigest)).ToArray();
            Require(digestAttributes.Length == 1 && digestAttributes[0].AttrValues.Count == 1, "messageDigest único");
            Require(Asn1OctetString.GetInstance(digestAttributes[0].AttrValues[0]).GetOctets().SequenceEqual(SHA256.HashData(signedBytes)), "messageDigest de ByteRange");
            var contentTypes = attributes.Where(a => a.AttrType.Equals(CmsAttributes.ContentType)).ToArray();
            Require(contentTypes.Length == 1 && contentTypes[0].AttrValues.Count == 1 &&
                DerObjectIdentifier.GetInstance(contentTypes[0].AttrValues[0]).Equals(CmsObjectIdentifiers.Data), "contentType data");

            var cms = new CmsSignedData(new CmsProcessableByteArray(signedBytes), encoded);
            var signer = cms.GetSignerInfos().GetSigners().Cast<SignerInformation>().Single();
            var matches = cms.GetCertificates("Collection").GetMatches(signer.SignerID).Cast<Org.BouncyCastle.X509.X509Certificate>().ToArray();
            Require(matches.Length == 1, "Leaf embebido e identificado sin ambigüedad");
            var leaf = matches[0];
            Require(sid.Name.GetEncoded().SequenceEqual(leaf.IssuerDN.GetEncoded()) && sid.SerialNumber.Value.Equals(leaf.SerialNumber), "SID issuer/serial exactos");
            Require(ess.GetCertHash().SequenceEqual(SHA256.HashData(leaf.GetEncoded())), "ESS hash del leaf");
            if (expected != null)
            {
                Require(ess.IssuerSerial != null, "Las nuevas firmas deben incluir issuerSerial");
                Require(leaf.GetEncoded().SequenceEqual(expected.RawData), "Certificado seleccionado: DER completo, subject, issuer, serial y SKI");
                var chain = FirmadorPades.Helpers.CertificateConverter.ToChain(expected);
                var embedded = cms.GetCertificates("Collection").GetMatches(null).Cast<Org.BouncyCastle.X509.X509Certificate>().ToArray();
                Require(chain.All(c => embedded.Any(e => e.GetEncoded().SequenceEqual(c.GetEncoded()))), "Cadena completa enviada a iText embebida");
                for (int i = 0; i < chain.Length - 1; i++)
                {
                    Require(chain[i].IssuerDN.Equivalent(chain[i + 1].SubjectDN), "Orden leaf a raíz");
                    chain[i].Verify(chain[i + 1].GetPublicKey());
                }
            }
            if (ess.IssuerSerial != null)
            {
                Require(ess.IssuerSerial.Serial.Value.Equals(leaf.SerialNumber), "ESS serial");
                Require(ess.IssuerSerial.Issuer.GetNames().Any(n => n.TagNo == GeneralName.DirectoryName &&
                    n.Name.GetDerEncoded().SequenceEqual(leaf.IssuerDN.GetDerEncoded())), "ESS issuer");
            }
            Require(signer.Verify(leaf), "Verificación RSA/CMS BouncyCastle");
            Require(util.ReadSignatureData(name).VerifySignatureIntegrityAndAuthenticity(), "Verificación iText");
            Console.WriteLine($"CMS {name}: OK; SHA256/RSA; ESS único; issuerSerial={ess.IssuerSerial != null}; leaf SHA256={Convert.ToHexString(SHA256.HashData(leaf.GetEncoded()))}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception($"Auditoría CMS: {message}");
    }
}
