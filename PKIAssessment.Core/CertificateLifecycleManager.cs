using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKIAssessment.Core
{

    public class CertificateLifecycleManager
    {
        private const int RSA_KEY_SIZE = 2048;
        private const string SIGNATURE_ALGORITHM = "SHA256WithRSA";
        private readonly SecureRandom _secureRandom;

        public CertificateLifecycleManager()
        {
            _secureRandom = new SecureRandom();
        }


        public AsymmetricCipherKeyPair GenerateKeyPair()
        {
            RsaKeyPairGenerator keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(_secureRandom, RSA_KEY_SIZE));
            return keyGen.GenerateKeyPair();
        }


        private BigInteger GenerateSerialNumber()
        {

            return new BigInteger(128, _secureRandom).Abs();
        }

        public X509Certificate IssueSelfSignedCertificate(
            AsymmetricCipherKeyPair keyPair,
            string subjectName,
            int validityDays = 3650)
        {
            var certGen = new X509V3CertificateGenerator();
            var subjectDN = new X509Name(subjectName);

            certGen.SetSerialNumber(GenerateSerialNumber());
            certGen.SetSubjectDN(subjectDN);
            certGen.SetIssuerDN(subjectDN);
            certGen.SetPublicKey(keyPair.Public);

            DateTime notBefore = DateTime.UtcNow;
            DateTime notAfter = notBefore.AddDays(validityDays);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);


            certGen.AddExtension(
                X509Extensions.BasicConstraints,
                critical: true,
                new BasicConstraints(true)
            );

            certGen.AddExtension(
                X509Extensions.KeyUsage,
                critical: true,
                new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign)
            );

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, keyPair.Private);
            return certGen.Generate(signatureFactory);
        }

        public string GenerateCSR(
            AsymmetricCipherKeyPair keyPair,
            string subjectName)
        {
            var csrObject = GenerateCSRObject(keyPair, subjectName);
            return ConvertToPem(csrObject.GetEncoded(), "CERTIFICATE REQUEST");
        }

        public Pkcs10CertificationRequest GenerateCSRObject(
            AsymmetricCipherKeyPair keyPair,
            string subjectName)
        {
            var subjectDN = new X509Name(subjectName);
            var csr = new Pkcs10CertificationRequest(
                SIGNATURE_ALGORITHM,
                subjectDN,
                keyPair.Public,
                null,
                keyPair.Private
            );
            return csr;
        }

        public X509Certificate IssueEndEntityCertificateFromCsr(
            X509Certificate caCert,
            AsymmetricKeyParameter caPrivateKey,
            Pkcs10CertificationRequest csr,
            long serialNumber,
            int validityDays = 365)
        {
            if (!csr.Verify())
                throw new InvalidOperationException("Invalid CSR signature.");

            var certGen = new X509V3CertificateGenerator();

            certGen.SetSerialNumber(BigInteger.ValueOf(serialNumber));
            certGen.SetIssuerDN(caCert.SubjectDN);
            
            var csrInfo = csr.GetCertificationRequestInfo();
            certGen.SetSubjectDN(csrInfo.Subject);
            certGen.SetPublicKey(csr.GetPublicKey());

            DateTime notBefore = DateTime.UtcNow;
            DateTime notAfter = notBefore.AddDays(validityDays);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);


            certGen.AddExtension(
                X509Extensions.BasicConstraints,
                critical: true,
                new BasicConstraints(false)
            );


            certGen.AddExtension(
                X509Extensions.KeyUsage,
                critical: true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation)
            );


            AddCrlDistributionPoint(certGen);

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, caPrivateKey);
            return certGen.Generate(signatureFactory);
        }

        public X509Certificate IssueEndEntityCertificate(
            X509Certificate caCert,
            AsymmetricKeyParameter caPrivateKey,
            AsymmetricKeyParameter endEntityPublicKey,
            string endEntitySubjectName,
            long serialNumber,
            int validityDays = 365)
        {
            var certGen = new X509V3CertificateGenerator();
            var subjectDN = new X509Name(endEntitySubjectName);

            certGen.SetSerialNumber(BigInteger.ValueOf(serialNumber));
            certGen.SetSubjectDN(subjectDN);
            certGen.SetIssuerDN(caCert.SubjectDN);
            certGen.SetPublicKey(endEntityPublicKey);

            DateTime notBefore = DateTime.UtcNow;
            DateTime notAfter = notBefore.AddDays(validityDays);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);

            certGen.AddExtension(
                X509Extensions.BasicConstraints,
                critical: true,
                new BasicConstraints(false)
            );

            certGen.AddExtension(
                X509Extensions.KeyUsage,
                critical: true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation)
            );

            AddCrlDistributionPoint(certGen);

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, caPrivateKey);
            return certGen.Generate(signatureFactory);
        }

        private void AddCrlDistributionPoint(X509V3CertificateGenerator certGen)
        {
            try
            {
                var dpn = new DistributionPointName(
                    new GeneralNames(
                        new GeneralName(
                            GeneralName.UniformResourceIdentifier,
                            "http://example.com/crl")));

                var dp = new DistributionPoint(dpn, null, null);
                certGen.AddExtension(
                    X509Extensions.CrlDistributionPoints,
                    critical: false,
                    new CrlDistPoint(new[] { dp }));
            }
            catch
            {

            }
        }

        public X509Crl GenerateEmptyCRL(X509Certificate caCert, AsymmetricKeyParameter caPrivateKey)
        {
            var crlGen = new X509V2CrlGenerator();
            crlGen.SetIssuerDN(caCert.SubjectDN);
            
            DateTime now = DateTime.UtcNow;
            crlGen.SetThisUpdate(now);
            crlGen.SetNextUpdate(now.AddDays(30));

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, caPrivateKey);
            return crlGen.Generate(signatureFactory);
        }

        public X509Crl RevokeAndUpdateCrl(
            X509Crl existingCrl,
            X509Certificate caCert,
            AsymmetricKeyParameter caPrivateKey,
            X509Certificate certToRevoke,
            int revocationReason = 0)
        {
            var crlGen = new X509V2CrlGenerator();
            crlGen.SetIssuerDN(caCert.SubjectDN);

            DateTime now = DateTime.UtcNow;
            crlGen.SetThisUpdate(now);
            crlGen.SetNextUpdate(now.AddDays(30));

            var existingEntries = existingCrl?.GetRevokedCertificates();
            if (existingEntries != null)
            {
                foreach (var entry in existingEntries)
                {
          
                    crlGen.AddCrlEntry(entry.SerialNumber, entry.RevocationDate, 0);
                }
            }

            crlGen.AddCrlEntry(certToRevoke.SerialNumber, now, revocationReason);

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, caPrivateKey);
            return crlGen.Generate(signatureFactory);
        }

        public X509Crl RevokeCertificateAndUpdateCRL(
            X509Certificate caCert,
            AsymmetricKeyParameter caPrivateKey,
            X509Certificate revokedCert,
            int revocationReason = 0)
        {
            var crlGen = new X509V2CrlGenerator();
            crlGen.SetIssuerDN(caCert.SubjectDN);
            
            DateTime now = DateTime.UtcNow;
            crlGen.SetThisUpdate(now);
            crlGen.SetNextUpdate(now.AddDays(30));

            crlGen.AddCrlEntry(revokedCert.SerialNumber, now, revocationReason);

            var signatureFactory = new Asn1SignatureFactory(SIGNATURE_ALGORITHM, caPrivateKey);
            return crlGen.Generate(signatureFactory);
        }

        private string ConvertToPem(byte[] data, string dataType)
        {
            string base64 = Convert.ToBase64String(data);
            var pem = new System.Text.StringBuilder();

            pem.AppendLine($"-----BEGIN {dataType}-----");

            const int lineLength = 64;
            for (int i = 0; i < base64.Length; i += lineLength)
            {
                int length = Math.Min(lineLength, base64.Length - i);
                pem.AppendLine(base64.Substring(i, length));
            }

            pem.AppendLine($"-----END {dataType}-----");
            return pem.ToString();
        }
    }
}
