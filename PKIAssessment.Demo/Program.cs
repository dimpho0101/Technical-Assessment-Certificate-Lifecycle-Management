using PKIAssessment.Core;
using Org.BouncyCastle.X509;
using System;

namespace PKIAssessment.Demo
{
    class Program
    {
        static void Main(string[] args)
        {

            var manager = new CertificateLifecycleManager();

     
            Console.WriteLine("Lifecycle Stage 1: Generating Root CA Key Pair (RSA 2048)...");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            var rootCAKeyPair = manager.GenerateKeyPair();
            Console.WriteLine(" Root CA Key Pair Generated Successfully");
            Console.WriteLine($"  - Public Key Type: {rootCAKeyPair.Public.GetType().Name}");
            Console.WriteLine($"  - Private Key Type: {rootCAKeyPair.Private.GetType().Name}\n");

       
            Console.WriteLine("Lifecycle Stage 2: Issuing Self-Signed Root CA Certificate...");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            string rootCASubjectName = "CN=MyRootCA,O=MyOrganization,C=SA";
            X509Certificate rootCACert = manager.IssueSelfSignedCertificate(
                rootCAKeyPair,
                rootCASubjectName,
                3650  
            );
            Console.WriteLine(" Root CA Certificate Issued");
            Console.WriteLine($"  - Subject: {rootCACert.SubjectDN}");
            Console.WriteLine($"  - Issuer: {rootCACert.IssuerDN}");
            Console.WriteLine($"  - Serial Number: {rootCACert.SerialNumber}");
            Console.WriteLine($"  - Not Before: {rootCACert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Not After: {rootCACert.NotAfter:yyyy-MM-dd HH:mm:ss}\n");

         
            Console.WriteLine("Lifecycle Stage 3: Generating End-Entity Key Pair...");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            var endEntityKeyPair = manager.GenerateKeyPair();
            string endEntitySubjectName = "CN=John.Doe,O=MyOrganization,C=SA";
            Console.WriteLine("End-Entity Key Pair Generated\n");

      
            Console.WriteLine("Lifecycle Stage 4: Generating CSR...");
            var csrObj = manager.GenerateCSRObject(endEntityKeyPair, endEntitySubjectName);
            var csrPem = manager.GenerateCSR(endEntityKeyPair, endEntitySubjectName);
            Console.WriteLine(" CSR Generated (PEM Format):");
            Console.WriteLine(csrPem);

       
            Console.WriteLine("\nLifecycle Stage 5: Issuing End-Entity Certificate from CSR...");
            var endEntityCert = manager.IssueEndEntityCertificateFromCsr(
                rootCACert,
                rootCAKeyPair.Private,
                csrObj,
                serialNumber: 1001,
                validityDays: 365
            );
            Console.WriteLine(" End-Entity Certificate Issued");
            Console.WriteLine($"  - Subject: {endEntityCert.SubjectDN}");
            Console.WriteLine($"  - Issuer: {endEntityCert.IssuerDN}");
            Console.WriteLine($"  - Serial Number: {endEntityCert.SerialNumber}");
            Console.WriteLine($"  - Not Before: {endEntityCert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Not After: {endEntityCert.NotAfter:yyyy-MM-dd HH:mm:ss}\n");

 
            Console.WriteLine("Lifecycle Stage 6: Generating Empty Certificate Revocation List (CRL)...");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            X509Crl emptyCRL = manager.GenerateEmptyCRL(rootCACert, rootCAKeyPair.Private);
            Console.WriteLine(" Empty CRL Generated");
            Console.WriteLine($"  - Issuer: {emptyCRL.IssuerDN}");
            Console.WriteLine($"  - This Update: {emptyCRL.ThisUpdate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Next Update: {emptyCRL.NextUpdate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Revoked Certificates Count: {emptyCRL.GetRevokedCertificates()?.Count ?? 0}\n");

    
            Console.WriteLine("Lifecycle Stage 7: Revoking End-Entity Certificate...");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            X509Crl updatedCRL = manager.RevokeCertificateAndUpdateCRL(
                rootCACert,
                rootCAKeyPair.Private,
                endEntityCert,
                revocationReason: 1  
            );
            Console.WriteLine(" Certificate Revoked and CRL Updated");
            Console.WriteLine($"  - Issuer: {updatedCRL.IssuerDN}");
            Console.WriteLine($"  - This Update: {updatedCRL.ThisUpdate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Next Update: {updatedCRL.NextUpdate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - Revoked Certificates Count: {updatedCRL.GetRevokedCertificates()?.Count ?? 0}");
            
            if (updatedCRL.GetRevokedCertificates() != null)
            {
                foreach (var revokedCert in updatedCRL.GetRevokedCertificates())
                {
                    Console.WriteLine($"    - Serial Number: {revokedCert.SerialNumber}");
                    Console.WriteLine($"    - Revocation Date: {revokedCert.RevocationDate:yyyy-MM-dd HH:mm:ss}\n");
                }
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("   PKI Certificate Lifecycle Demo Completed Successfully!");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
        }
    }
}
