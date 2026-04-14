#!/usr/bin/env python3
import argparse
import base64
import json
import sys
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import List, Optional

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID


DEFAULT_JWKS_URL = "https://inferno.healthit.gov/suites/custom/smart_stu2/.well-known/jwks.json"


def b64url_to_int(value: str) -> int:
    padding = "=" * ((4 - len(value) % 4) % 4)
    data = base64.urlsafe_b64decode(value + padding)
    return int.from_bytes(data, "big")


def der_len(length: int) -> bytes:
    if length < 0x80:
        return bytes([length])

    encoded = []
    while length:
        encoded.append(length & 0xFF)
        length >>= 8
    encoded.reverse()
    return bytes([0x80 | len(encoded)]) + bytes(encoded)


def der_integer(value: int) -> bytes:
    if value == 0:
        body = b"\x00"
    else:
        body = value.to_bytes((value.bit_length() + 7) // 8, "big")
        if body[0] & 0x80:
            body = b"\x00" + body
    return b"\x02" + der_len(len(body)) + body


def der_sequence(parts: List[bytes]) -> bytes:
    body = b"".join(parts)
    return b"\x30" + der_len(len(body)) + body


def der_bit_string(body: bytes) -> bytes:
    prefixed = b"\x00" + body
    return b"\x03" + der_len(len(prefixed)) + prefixed


def build_subject_public_key_info(n_value: int, e_value: int) -> bytes:
    rsa_public_key = der_sequence([der_integer(n_value), der_integer(e_value)])
    algorithm_identifier = bytes.fromhex("300d06092a864886f70d0101010500")
    return der_sequence([algorithm_identifier, der_bit_string(rsa_public_key)])


def pem_wrap(label: str, der_bytes: bytes) -> str:
    encoded = base64.encodebytes(der_bytes).decode("ascii").replace("\n", "")
    lines = [encoded[i:i + 64] for i in range(0, len(encoded), 64)]
    return f"-----BEGIN {label}-----\n" + "\n".join(lines) + f"\n-----END {label}-----\n"


def fetch_jwks(url: str) -> dict:
    with urllib.request.urlopen(url) as response:
        return json.loads(response.read().decode("utf-8"))


def select_rsa_key(jwks: dict, kid: Optional[str]) -> dict:
    keys = jwks.get("keys", [])
    rsa_keys = [key for key in keys if key.get("kty") == "RSA"]
    if not rsa_keys:
        raise ValueError("No RSA keys found in the JWKS.")

    if kid:
        for key in rsa_keys:
            if key.get("kid") == kid:
                return key
        raise ValueError(f"No RSA key with kid '{kid}' found in the JWKS.")

    return rsa_keys[0]


def write_text(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8")


def write_json(path: Path, content: object) -> None:
    path.write_text(json.dumps(content, indent=2), encoding="utf-8")


def build_published_jwks(source_jwks: dict, selected_key: dict, mode: str) -> dict:
    if mode == "selected-rsa":
        return {"keys": [selected_key]}

    if mode == "full-source":
        return source_jwks

    raise ValueError(f"Unsupported OIDC JWKS mode '{mode}'.")


def collect_signing_algs(jwks: dict) -> List[str]:
    algs = sorted({key.get("alg") for key in jwks.get("keys", []) if key.get("alg")})
    return [alg for alg in algs if alg]


def normalize_url(url: str) -> str:
    return url.rstrip("/")


def write_oidc_package(
    output_dir: Path,
    issuer_url: str,
    source_jwks: dict,
    selected_key: dict,
    jwks_mode: str,
    federated_audience: str,
    federated_subject: Optional[str],
) -> Path:
    issuer = normalize_url(issuer_url)
    oidc_dir = output_dir / "inferno-oidc-issuer"
    well_known_dir = oidc_dir / ".well-known"
    well_known_dir.mkdir(parents=True, exist_ok=True)

    published_jwks = build_published_jwks(source_jwks, selected_key, jwks_mode)
    signing_algs = collect_signing_algs(published_jwks)

    openid_configuration = {
        "issuer": issuer,
        "jwks_uri": f"{issuer}/jwks.json",
        "response_types_supported": ["id_token"],
        "subject_types_supported": ["public"],
        "id_token_signing_alg_values_supported": signing_algs,
    }

    sample_federated_credential = {
        "name": "inferno-custom-oidc-trust",
        "issuer": issuer,
        "subject": federated_subject or "__SET_INFERNO_TOKEN_SUBJECT__",
        "audiences": [federated_audience],
        "description": "Sample federated credential payload for testing a custom OIDC trust using Inferno JWKS.",
    }

    write_json(well_known_dir / "openid-configuration", openid_configuration)
    write_json(well_known_dir / "openid-configuration.json", openid_configuration)
    write_json(oidc_dir / "jwks.json", published_jwks)
    write_json(oidc_dir / "source-jwks.json", source_jwks)
    write_json(oidc_dir / "federated-credential-sample.json", sample_federated_credential)
    write_text(
        oidc_dir / "README.txt",
        "\n".join(
            [
                "Inferno custom OIDC trust issuer package",
                "",
                f"Issuer URL: {issuer}",
                f"JWKS mode: {jwks_mode}",
                f"Published algorithms: {', '.join(signing_algs) if signing_algs else '(none declared)'}",
                "",
                "Files:",
                "- .well-known/openid-configuration: OIDC discovery document",
                "- jwks.json: Published JWKS for the custom issuer",
                "- source-jwks.json: Original fetched Inferno JWKS",
                "- federated-credential-sample.json: Sample trust payload for Microsoft Entra workload federation tests",
                "",
                "Important:",
                "- Host these files at a public HTTPS URL whose base exactly matches the issuer value.",
                "- This package proves OIDC discovery + JWKS hosting mechanics only.",
                "- It does NOT by itself make Inferno assertions valid for workload identity federation.",
                "- The incoming external token still needs issuer/subject/audience claims that match the federated credential definition.",
                "- Inferno SMART client assertions typically use SMART-specific iss/sub/aud values, so a claim-shape mismatch remains likely.",
            ]
        ),
    )

    return oidc_dir


def parse_subject(subject: str) -> x509.Name:
    values = []
    for part in subject.split("/"):
        if not part:
            continue
        key, value = part.split("=", 1)
        if key == "CN":
            values.append(x509.NameAttribute(NameOID.COMMON_NAME, value))
        else:
            raise ValueError(f"Unsupported subject attribute '{key}'. Only CN is supported.")
    if not values:
        raise ValueError("Subject must include at least /CN=<value>.")
    return x509.Name(values)


def generate_certificate(n_value: int, e_value: int, cert_pem: Path, cert_der: Path, signer_key: Path, subject: str, days: int) -> None:
    inferno_public_key = rsa.RSAPublicNumbers(e_value, n_value).public_key()
    signer_private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    now = datetime.now(timezone.utc)
    subject_name = parse_subject(subject)
    issuer_name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "Local Inferno JWKS Upload Test Issuer")])

    certificate = (
        x509.CertificateBuilder()
        .subject_name(subject_name)
        .issuer_name(issuer_name)
        .public_key(inferno_public_key)
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(minutes=5))
        .not_valid_after(now + timedelta(days=days))
        .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
        .add_extension(x509.SubjectKeyIdentifier.from_public_key(inferno_public_key), critical=False)
        .sign(private_key=signer_private_key, algorithm=hashes.SHA256())
    )

    cert_pem.write_bytes(certificate.public_bytes(serialization.Encoding.PEM))
    cert_der.write_bytes(certificate.public_bytes(serialization.Encoding.DER))
    signer_key.write_bytes(
        signer_private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=serialization.NoEncryption(),
        )
    )


def maybe_upload_certificate(app_id: str, cert_path: Path) -> None:
    import subprocess

    process = subprocess.run(
        [
            "az",
            "ad",
            "app",
            "credential",
            "reset",
            "--id",
            app_id,
            "--cert",
            f"@{cert_path}",
            "--append",
        ],
        capture_output=True,
        text=True,
    )
    if process.returncode != 0:
        raise RuntimeError(
            f"Azure CLI upload failed.\nSTDOUT:\n{process.stdout}\nSTDERR:\n{process.stderr}"
        )


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Fetch Inferno JWKS, convert the RSA key to PEM/X.509 artifacts, and optionally emit a minimal OIDC issuer package or upload the public cert to Entra."
    )
    parser.add_argument("--jwks-url", default=DEFAULT_JWKS_URL, help="JWKS URL to fetch")
    parser.add_argument("--kid", help="Specific RSA kid to use from the JWKS")
    parser.add_argument("--output-dir", default="inferno-jwks-artifacts", help="Directory to write generated artifacts")
    parser.add_argument("--subject", default="/CN=Inferno JWKS Upload Test", help="Subject for the generated X.509 certificate")
    parser.add_argument("--days", type=int, default=365, help="Number of days for the generated certificate")
    parser.add_argument("--oidc-issuer-url", help="If provided, create a minimal static OIDC issuer package for the fetched Inferno JWKS.")
    parser.add_argument(
        "--oidc-jwks-mode",
        choices=["selected-rsa", "full-source"],
        default="selected-rsa",
        help="JWKS content to publish in the generated OIDC issuer package.",
    )
    parser.add_argument(
        "--federated-audience",
        default="api://AzureADTokenExchange",
        help="Audience value to place in the sample federated credential JSON.",
    )
    parser.add_argument("--federated-subject", help="Optional subject value to place in the sample federated credential JSON.")
    parser.add_argument("--app-id", help="Entra app registration appId/clientId for optional upload")
    parser.add_argument("--upload", action="store_true", help="Upload the generated public certificate to the Entra app registration")
    args = parser.parse_args()

    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    jwks = fetch_jwks(args.jwks_url)
    selected_key = select_rsa_key(jwks, args.kid)

    n_value = b64url_to_int(selected_key["n"])
    e_value = b64url_to_int(selected_key["e"])
    spki_der = build_subject_public_key_info(n_value, e_value)
    public_key_pem = pem_wrap("PUBLIC KEY", spki_der)

    public_key_path = output_dir / "inferno-rsa-public-key.pem"
    selected_key_path = output_dir / "selected-inferno-rsa-jwk.json"
    cert_pem_path = output_dir / "inferno-rsa-upload-cert.pem"
    cert_der_path = output_dir / "inferno-rsa-upload-cert.cer"
    signer_key_path = output_dir / "local-signer-private-key.pem"
    notes_path = output_dir / "README.txt"

    write_text(public_key_path, public_key_pem)
    write_json(selected_key_path, selected_key)
    generate_certificate(n_value, e_value, cert_pem_path, cert_der_path, signer_key_path, args.subject, args.days)

    write_text(
        notes_path,
        "\n".join(
            [
                "Inferno JWKS upload experiment artifacts",
                "",
                f"JWKS source: {args.jwks_url}",
                f"Selected kid: {selected_key.get('kid')}",
                f"Selected alg: {selected_key.get('alg')}",
                "",
                "Files:",
                f"- {public_key_path.name}: SubjectPublicKeyInfo PEM generated from the RSA JWK",
                f"- {cert_pem_path.name}: X.509 PEM wrapping the Inferno RSA public key",
                f"- {cert_der_path.name}: DER/cer form of the same certificate for Entra upload tests",
                f"- {signer_key_path.name}: Local signer key used only to create the wrapper certificate",
                "",
                "Important:",
                "- This proves public-key registration mechanics only.",
                "- It does not prove that Entra will accept Inferno's full SMART-style client assertion format or RS384 semantics.",
            ]
        ),
    )

    oidc_package_path = None
    if args.oidc_issuer_url:
        oidc_package_path = write_oidc_package(
            output_dir=output_dir,
            issuer_url=args.oidc_issuer_url,
            source_jwks=jwks,
            selected_key=selected_key,
            jwks_mode=args.oidc_jwks_mode,
            federated_audience=args.federated_audience,
            federated_subject=args.federated_subject,
        )

    if args.upload:
        if not args.app_id:
            raise ValueError("--app-id is required when --upload is used.")
        maybe_upload_certificate(args.app_id, cert_der_path)

    print(json.dumps(
        {
            "outputDir": str(output_dir),
            "selectedKid": selected_key.get("kid"),
            "selectedAlg": selected_key.get("alg"),
            "publicKeyPem": str(public_key_path),
            "uploadCertPem": str(cert_pem_path),
            "uploadCertCer": str(cert_der_path),
            "oidcIssuerPackage": str(oidc_package_path) if oidc_package_path else None,
            "uploaded": bool(args.upload),
        },
        indent=2,
    ))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1)
