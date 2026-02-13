using Xunit;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using PKIAssessment.Core;
using System;

namespace PKIAssessment.Tests
{
    public class CertificateLifecycleManagerTests
    {
        private readonly CertificateLifecycleManager _manager = new CertificateLifecycleManager();

        #region Key Generation Tests

        [Fact]
        public void GenerateKeyPair_ShouldReturnValidAsymmetricCipherKeyPair()
        {
            var keyPair = _manager.GenerateKeyPair();

            Assert.NotNull(keyPair);
            Assert.NotNull(keyPair.Public);
            Assert.NotNull(keyPair.Private);
        }

        [Fact]
        public void GenerateKeyPair_ShouldGenerateUniquePairs()
        {
            var keyPair1 = _manager.GenerateKeyPair();
            var keyPair2 = _manager.GenerateKeyPair();

            var rsaKey1 = (RsaKeyParameters)keyPair1.Public;
            var rsaKey2 = (RsaKeyParameters)keyPair2.Public;

            Assert.NotEqual(rsaKey1.Modulus.ToString(), rsaKey2.Modulus.ToString());
        }

        #endregion

        #region Self-Signed Certificate Tests

        [Fact]
        public void IssueSelfSignedCertificate_ShouldCreateValidCertificate()
        {
            var keyPair = _manager.GenerateKeyPair();
            string subjectName = "CN=TestCA,O=TestOrganization,C=SA";

            var cert = _manager.IssueSelfSignedCertificate(keyPair, subjectName);

            Assert.NotNull(cert);
            Assert.Equal(cert.SubjectDN, cert.IssuerDN); 
            Assert.Contains("TestCA", cert.SubjectDN.ToString());
        }

        [Fact]
        public void IssueSelfSignedCertificate_ShouldHavePositiveSerialNumber()
        {
            var keyPair = _manager.GenerateKeyPair();
            string subjectName = "CN=TestCA,O=TestOrg,C=SA";

            var cert = _manager.IssueSelfSignedCertificate(keyPair, subjectName);

            Assert.NotNull(cert.SerialNumber);
            Assert.True(cert.SerialNumber.CompareTo(BigInteger.Zero) > 0);
        }

        [Fact]
        public void IssueSelfSignedCertificate_ShouldHaveValidityPeriod()
        {
            var keyPair = _manager.GenerateKeyPair();
            string subjectName = "CN=TestCA,O=TestOrg,C=SA";
            int validityDays = 365;

            var cert = _manager.IssueSelfSignedCertificate(keyPair, subjectName, validityDays);

            Assert.True(cert.NotBefore < cert.NotAfter);

            var actualDays = (cert.NotAfter - cert.NotBefore).TotalDays;
            Assert.InRange(actualDays, validityDays - 1, validityDays + 1);
        }

        [Fact]
        public void IssueSelfSignedCertificate_CertificateShouldBeValidNow()
        {
            var keyPair = _manager.GenerateKeyPair();
            string subjectName = "CN=TestCA,O=TestOrg,C=SA";

            var cert = _manager.IssueSelfSignedCertificate(keyPair, subjectName);

            var now = DateTime.UtcNow;
            Assert.True(cert.NotBefore <= now);
            Assert.True(cert.NotAfter >= now);
        }

        #endregion

        #region CRL Generation Tests

        [Fact]
        public void GenerateEmptyCRL_ShouldCreateValidCRL()
        {
            var keyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(keyPair, "CN=TestCA,O=TestOrg,C=SA");

            var crl = _manager.GenerateEmptyCRL(caCert, keyPair.Private);

            Assert.NotNull(crl);
            Assert.Contains("TestCA", crl.IssuerDN.ToString());
            Assert.True(crl.ThisUpdate < crl.NextUpdate);
        }

        [Fact]
        public void GenerateEmptyCRL_ShouldHaveNoRevokedCertificates()
        {
            var keyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(keyPair, "CN=TestCA,O=TestOrg,C=SA");

            var crl = _manager.GenerateEmptyCRL(caCert, keyPair.Private);

            var revoked = crl.GetRevokedCertificates();
            Assert.True(revoked == null || revoked.Count == 0);
        }

        #endregion

        #region CSR Tests

        [Fact]
        public void GenerateCSR_ShouldReturnPemWithCorrectHeaders()
        {
            var keyPair = _manager.GenerateKeyPair();
            string subjectName = "CN=EndEntity,O=TestOrg,C=SA";

            string csrPem = _manager.GenerateCSR(keyPair, subjectName);

            Assert.NotNull(csrPem);
            Assert.Contains("-----BEGIN CERTIFICATE REQUEST-----", csrPem);
            Assert.Contains("-----END CERTIFICATE REQUEST-----", csrPem);
        }

        [Fact]
        public void GenerateCSRObject_ShouldVerifySignature()
        {
            var keyPair = _manager.GenerateKeyPair();
            var csrObj = _manager.GenerateCSRObject(keyPair, "CN=EndEntity,O=TestOrg,C=SA");

            Assert.True(csrObj.Verify());
        }

        #endregion

        #region End-Entity Certificate Tests

        [Fact]
        public void IssueEndEntityCertificate_ShouldCreateValidEECertificate()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");

            var eeKeyPair = _manager.GenerateKeyPair();
            string eeSubjectName = "CN=EndEntity,O=TestOrg,C=SA";

            var eeCert = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair.Public, eeSubjectName, serialNumber: 100);

            Assert.NotNull(eeCert);
            Assert.Contains("EndEntity", eeCert.SubjectDN.ToString());
            Assert.Contains("TestCA", eeCert.IssuerDN.ToString());
            Assert.NotEqual(eeCert.SubjectDN, eeCert.IssuerDN);
        }

        [Fact]
        public void IssueEndEntityCertificateFromCsr_ShouldVerifyAgainstCaPublicKey()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");

            var eeKeyPair = _manager.GenerateKeyPair();
            var csr = _manager.GenerateCSRObject(eeKeyPair, "CN=EndEntity,O=TestOrg,C=SA");

            var eeCert = _manager.IssueEndEntityCertificateFromCsr(
                caCert, caKeyPair.Private, csr, serialNumber: 101);


            eeCert.Verify(caCert.GetPublicKey());
        }

        [Fact]
        public void IssueEndEntityCertificate_ShouldHaveDifferentSerialNumbers()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");
            var eeKeyPair = _manager.GenerateKeyPair();

            var eeCert1 = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair.Public, "CN=EE1,O=TestOrg,C=SA", 102);

            var eeCert2 = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair.Public, "CN=EE2,O=TestOrg,C=SA", 103);

            Assert.NotEqual(eeCert1.SerialNumber, eeCert2.SerialNumber);
        }

        #endregion

        #region Certificate Revocation Tests

        [Fact]
        public void RevokeCertificateAndUpdateCRL_ShouldAddRevokedCertificateToList()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");

            var eeKeyPair = _manager.GenerateKeyPair();
            var eeCert = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair.Public, "CN=EndEntity,O=TestOrg,C=SA", 200);

            var crl = _manager.RevokeCertificateAndUpdateCRL(caCert, caKeyPair.Private, eeCert, revocationReason: 1);

            var revoked = crl.GetRevokedCertificates();
            Assert.NotNull(revoked);
            Assert.True(revoked.Count > 0);

            bool found = false;
            foreach (var entry in revoked)
            {
                if (entry.SerialNumber.Equals(eeCert.SerialNumber))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found);
        }

        [Fact]
        public void RevokeAndUpdateCrl_ShouldAccumulateRevocations()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");

            var crl = _manager.GenerateEmptyCRL(caCert, caKeyPair.Private);

            var eeKeyPair1 = _manager.GenerateKeyPair();
            var eeCert1 = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair1.Public, "CN=EE1,O=TestOrg,C=SA", 300);

            var eeKeyPair2 = _manager.GenerateKeyPair();
            var eeCert2 = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair2.Public, "CN=EE2,O=TestOrg,C=SA", 301);

            crl = _manager.RevokeAndUpdateCrl(crl, caCert, caKeyPair.Private, eeCert1, revocationReason: 1);
            crl = _manager.RevokeAndUpdateCrl(crl, caCert, caKeyPair.Private, eeCert2, revocationReason: 1);

            var revoked = crl.GetRevokedCertificates();
            Assert.NotNull(revoked);
            Assert.Equal(2, revoked.Count);
        }

        [Fact]
        public void RevokeCertificateAndUpdateCRL_CRLShouldContainCorrectIssuer()
        {
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=TestCA,O=TestOrg,C=SA");

            var eeKeyPair = _manager.GenerateKeyPair();
            var eeCert = _manager.IssueEndEntityCertificate(
                caCert, caKeyPair.Private, eeKeyPair.Public, "CN=EndEntity,O=TestOrg,C=SA", 400);

            var crl = _manager.RevokeCertificateAndUpdateCRL(caCert, caKeyPair.Private, eeCert);

            Assert.Equal(caCert.SubjectDN, crl.IssuerDN);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void FullCertificateLifecycle_EndToEnd_UsesCsrAndCumulativeCrl()
        {
        
            var caKeyPair = _manager.GenerateKeyPair();
            var caCert = _manager.IssueSelfSignedCertificate(caKeyPair, "CN=RootCA,O=TestOrganization,C=SA", 3650);

         
            var crl = _manager.GenerateEmptyCRL(caCert, caKeyPair.Private);
            Assert.True(crl.GetRevokedCertificates() == null || crl.GetRevokedCertificates().Count == 0);


            var eeKeyPair = _manager.GenerateKeyPair();
            var csrObj = _manager.GenerateCSRObject(eeKeyPair, "CN=user@example.com,O=TestOrganization,C=SA");
            Assert.True(csrObj.Verify());

     
            var eeCert = _manager.IssueEndEntityCertificateFromCsr(caCert, caKeyPair.Private, csrObj, serialNumber: 500);
            eeCert.Verify(caCert.GetPublicKey()); 

           
            crl = _manager.RevokeAndUpdateCrl(crl, caCert, caKeyPair.Private, eeCert, revocationReason: 1);

            var revoked = crl.GetRevokedCertificates();
            Assert.NotNull(revoked);
            Assert.True(revoked.Count > 0);

            bool found = false;
            foreach (var entry in revoked)
            {
                if (entry.SerialNumber.Equals(eeCert.SerialNumber))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found);
        }

        #endregion
    }
}