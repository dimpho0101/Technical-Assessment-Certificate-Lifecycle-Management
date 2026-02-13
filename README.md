## PKI Certificate Lifecycle Management
## Overview of My Approach

# For this assessment, my goal was to implement a clean, realistic PKI lifecycle rather than just generate certificates programmatically.

# I structured the solution around the actual certificate flow used in practice:

Generate RSA key pairs (2048-bit).
Create a self-signed Root CA certificate.
Generate a Certificate Signing Request (CSR) for an end entity.
Verify the CSR before issuing a certificate.
Issue an end-entity certificate signed by the CA.
Generate an empty CRL.
Revoke the certificate and update the CRL cumulatively.
I used BouncyCastle because it provides full control over certificate creation, extensions, and CRL management, which the built-in .NET libraries do not fully support.
I focused on correctness and clarity over unnecessary abstraction.

##Design Decisions & Assumptions 
#Security Choices

I used:
RSA 2048-bit keys
SHA-256 for signatures
SecureRandom for serial numbers
Serial numbers for CA certificates are generated using secure random values to avoid predictability.
CSR signatures are verified before issuing certificates to ensure authenticity.
Certificates include appropriate extensions:
Root CA: BasicConstraints (CA=true) and KeyCertSign/CrlSign
End-entity: BasicConstraints (CA=false), DigitalSignature, NonRepudiation, and a CRL Distribution Point

## CRL Management

# CRLs are handled cumulatively. When a certificate is revoked, previous revocations are preserved and new entries are appended. This reflects realistic CA behavior.

## Testing Approach

I wrote unit tests to validate:

Key generation integrity
Self-signed certificate correctness
CSR validity
Certificate signature verification against the CA

CRL generation and revocation behavior

End-to-end lifecycle flow

Where possible, I verified cryptographic validity (e.g., csr.Verify() and eeCert.Verify(caCert.GetPublicKey())) rather than just checking object properties.

## Build, Test, and Run Instructions
### 1. Build the Application

dotnet build


### 2. Run Tests

dotnet test

Expected: **17/17 tests passing**

### 3. Run the Demo

dotnet run --project PKIAssessment.Demo

## What You'll See

1. **Generate Root CA Key Pair** (RSA 2048-bit)
2. **Issue Self-Signed Root CA Certificate** (10 years validity)
3. **Generate End-Entity Key Pair**
4. **Generate Certificate Signing Request (CSR)**
5. **Issue End-Entity Certificate from CSR** (1 year validity)
6. **Generate Empty Certificate Revocation List (CRL)**
7. **Revoke Certificate and Update CRL**

## Test Coverage

- Key generation (3 tests)
- Self-signed certificates (4 tests)
- CRL generation (3 tests)
- End-entity certificates (4 tests)
- Certificate revocation (3 tests)
- Full lifecycle (1 test)



